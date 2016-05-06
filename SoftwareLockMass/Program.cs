﻿using CSMSL.IO.MzML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSMSL.IO.Thermo;
using CSMSL.IO;
using CSMSL.Spectral;
using System.IO;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using CSMSL;
using CSMSL.Proteomics;
using CSMSL.Chemistry;

namespace SoftwareLockMass
{
    class Program
    {
        // THIS PARAMETER IS FRAGILE!!!
        // TUNED TO CORRESPOND TO SPECTROMETER OUTPUT
        // BETTER SPECTROMETERS WOULD HAVE BETTER (LOWER) RESOLUIONS
        // Parameter for isotopolouge distribution searching
        private const double fineResolution = 0.08;

        // My parameters!
        private const int numIsotopologuesToConsider = 10;
        private const double toleranceInMZforIsotopologueSearch = 0.01;
        private const int numIsotopologuesNeededToBeConsideredIdentified = 2;
        private const int numChargesNeededToBeConsideredIdentified = 2;
        // NEED TO TRY 1e3!!
        private const double intensityCutoff = 1e3; // 1e5 is too sparse. 1e4 is nice. NEED TO TRY 1e3!! 0 is a noisy mess
        private const double thresholdPassParameter = 0.01; // Or double.MaxValue or 0.01?


        // Whether to do calibration, or to just write the file in my mzML way
        private const bool calibrate = true;

        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to my software lock mass implementation");

            Console.WriteLine("Reading uncalibrated raw/mzML file");

            //IMSDataFile<ISpectrum<IPeak>> myMSDataFile = new ThermoRawFile(@"E:\Stefan\data\jurkat\120426_Jurkat_highLC_Frac1.raw");
            IMSDataFile<ISpectrum<IPeak>> myMSDataFile = new Mzml(@"E:\Stefan\data\jurkat\MyUncalibrated.mzML");

            myMSDataFile.Open();

            int numToCreate = myMSDataFile.LastSpectrumNumber;

            // Code to list spectrum indices of MS1 spectra
            //for (int i = 0; i < myMSDataFile.LastSpectrumNumber; i++)
            //{
            //    if (myMSDataFile[i + 1].MsnOrder == 1)
            //    {
            //        Console.WriteLine((i + 1));
            //    }
            //}

            Console.WriteLine("Spectrum number 2810, peak num 2394:");
            Console.WriteLine(myMSDataFile[2810].MassSpectrum.GetPeak(2393));
            Console.WriteLine("Spectrum number 2813:");
            Console.WriteLine(myMSDataFile[2813].id);
            Console.WriteLine(myMSDataFile[2813].SelectedIonMonoisotopicMZ);

            List<CalibratedSpectrum> calibratedSpectra;

            if (calibrate)
            {
                Console.WriteLine("Performing calibration");
                string pepFileLocation = @"E:\Stefan\data\morpheusmzMLoutput1\MyUncalibrated.mzid";
                calibratedSpectra = Calibrate(myMSDataFile, pepFileLocation, numToCreate);
            }
            else
            {
                Console.WriteLine("Not calibrating, just copying mass arrays");
                calibratedSpectra = new List<CalibratedSpectrum>();
                for (int i = 0; i < numToCreate; i++)
                {
                    calibratedSpectra.Add(new CalibratedSpectrum());
                    calibratedSpectra[i].AddMZValues(myMSDataFile[i + 1].MassSpectrum.GetMasses());
                }
            }


            Console.WriteLine("Creating _indexedmzMLConnection, and putting data in it");

            indexedmzML _indexedmzMLConnection = GetMyIndexedMZml(myMSDataFile, calibratedSpectra, numToCreate);

            Console.WriteLine("Writing calibrated mzML file");

            string outputFilePath = @"E:\Stefan\data\CalibratedOutput\calibratedOutput.mzML";

            Mzml.Write(outputFilePath, _indexedmzMLConnection);

            Console.WriteLine("Reading calibrated mzML file for verification");

            Mzml mzmlFile2 = new Mzml(outputFilePath);

            mzmlFile2.Open();

            Console.WriteLine("Number of spectra:{0}", mzmlFile2.LastSpectrumNumber);


            Console.WriteLine("Spectrum number 2810, peak num 2394:");
            Console.WriteLine(mzmlFile2[2810].MassSpectrum.GetPeak(2393));
            Console.WriteLine("Spectrum number 2813:");
            Console.WriteLine(mzmlFile2[2813].id);
            Console.WriteLine(mzmlFile2[2813].SelectedIonMonoisotopicMZ);

            Console.WriteLine("Finished running my software lock mass implementation");
            Console.Read();
        }

        private static List<CalibratedSpectrum> Calibrate(IMSDataFile<ISpectrum<IPeak>> myMSDataFile, string pepFileLocation, int numToCreate)
        {
            XmlSerializer _indexedSerializer = new XmlSerializer(typeof(mzIdentML.MzIdentMLType));
            Stream stream = new FileStream(pepFileLocation, FileMode.Open);
            // Read the XML file into the variable
            mzIdentML.MzIdentMLType dd = _indexedSerializer.Deserialize(stream) as mzIdentML.MzIdentMLType;

            // Get the training data out of xml
            List<double[]> trainingData = new List<double[]>();
            List<double> labelData = new List<double>();
            bool passThreshold=true;


            XmlSerializer unimodSerializer = new XmlSerializer(typeof(unimod));
            Stream stream2 = new FileStream(@"E:\Stefan\data\Unimod\unimod_tables.xml", FileMode.Open);
            // Read the XML file into the variable
            unimod unimodDeserialized = unimodSerializer.Deserialize(stream2) as unimod;

            // Loop over all results from the mzIdentML file
            for (int matchIndex = 0; matchIndex < dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult.Length && passThreshold == true; matchIndex++) { 
                if (dd.SequenceCollection.PeptideEvidence[matchIndex].isDecoy)
                {
                    continue;
                }

                double experimentalMassToCharge = dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].SpectrumIdentificationItem[0].experimentalMassToCharge;
                double calculatedMassToCharge = dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].SpectrumIdentificationItem[0].calculatedMassToCharge;
                int chargeState = dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].SpectrumIdentificationItem[0].chargeState;
                string spectrumID = dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].spectrumID;


                //passThreshold = dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].SpectrumIdentificationItem[0].passThreshold;

                passThreshold = (Convert.ToDouble(dd.DataCollection.AnalysisData.SpectrumIdentificationList[0].SpectrumIdentificationResult[matchIndex].SpectrumIdentificationItem[0].cvParam[0].value) < thresholdPassParameter);

                int spectrumIndex = GetSpectrumIndexFromSpectrumID(spectrumID);
                double precursorRetentionTime = myMSDataFile[GetSpectrumIndexFromSpectrumID(myMSDataFile[spectrumIndex].PrecursorID)].RetentionTime;
                var errorInMZ = experimentalMassToCharge - calculatedMassToCharge;


                Peptide peptide1 = new Peptide(dd.SequenceCollection.Peptide[matchIndex].PeptideSequence);
                if (dd.SequenceCollection.Peptide[matchIndex].Modification != null)
                {
                    for (int i = 0; i < dd.SequenceCollection.Peptide[matchIndex].Modification.Length; i++)
                    {
                        var residueNumber = dd.SequenceCollection.Peptide[matchIndex].Modification[i].location;
                        string unimodAcession = dd.SequenceCollection.Peptide[matchIndex].Modification[i].cvParam[0].accession;
                        var indexToLookFor = GetLastNumberFromString(unimodAcession) - 1;
                        while (unimodDeserialized.modifications[indexToLookFor].record_id != GetLastNumberFromString(unimodAcession))
                            indexToLookFor--;
                        string theFormula = unimodDeserialized.modifications[indexToLookFor].composition;
                        ChemicalFormulaModification modification = new ChemicalFormulaModification(ConvertToCSMSLFormula(theFormula));
                        peptide1.AddModification(modification, residueNumber);
                    }

                }
                IsotopicDistribution dist = new IsotopicDistribution(fineResolution);
                var fullSpectrum = dist.CalculateDistribuition(peptide1.GetChemicalFormula());
                var distributionSpectrum = fullSpectrum.FilterByNumberOfMostIntense(Math.Min(numIsotopologuesToConsider, fullSpectrum.Count));

                var fullMS1spectrum = myMSDataFile[GetSpectrumIndexFromSpectrumID(myMSDataFile[spectrumIndex].PrecursorID)];

                var rangeOfSpectrum = fullMS1spectrum.MzRange;

                var spectrumOfHighIntensities = fullMS1spectrum.MassSpectrum.FilterByIntensity(intensityCutoff, double.MaxValue);

                // Now search in the spectrum for peaks corresponding to the distribution, for the different charges


                List<double[]> consideredChargeTrainingPoints = new List<double[]>();
                List<double> consideredChargeLabels = new List<double>();
                int numIdentifiedCharges = 0;
                for (int chargeToLookAt = 1; ; chargeToLookAt++)
                {
                    Spectrum<MZPeak> chargedDistribution = distributionSpectrum.CorrectMasses(s => (s + chargeToLookAt * Constants.Proton) / chargeToLookAt);

                    // Skip charge if it's too high
                    if (chargedDistribution.LastMZ > rangeOfSpectrum.Maximum)
                        continue;
                    // Stop if going too low
                    if (chargedDistribution.GetBasePeak().MZ < rangeOfSpectrum.Minimum)
                        break;

                    int numIdentifiedIsotopologues = 0;


                    List<double[]> consideredIsotopologueTrainingPoints = new List<double[]>();
                    List<double> consideredIsotopologueLabels = new List<double>();
                    // Look for individual isotopologues
                    for (int isotopologueIndex = 0; isotopologueIndex < numIsotopologuesToConsider; isotopologueIndex++)
                    {
                        var closestPeak = spectrumOfHighIntensities.GetClosestPeak(chargedDistribution[isotopologueIndex].MZ);

                        // intensityCutoff


                        if (Math.Abs(chargedDistribution[isotopologueIndex].MZ - closestPeak.X) < Math.Max(toleranceInMZforIsotopologueSearch, Math.Abs(errorInMZ)))
                        {
                            // Identified one!!! Woohoo.

                            numIdentifiedIsotopologues++;

                            double experimentalMassToChargeOfIsotopologue = closestPeak.X;
                            double retentionTimeOfIsotopologue = precursorRetentionTime;
                            double calculatedMassToChargeOfIsotopologue = chargedDistribution[isotopologueIndex].MZ;

                            double[] trainingPointFromIsotopologue = new double[3];
                            trainingPointFromIsotopologue[0] = 1;
                            trainingPointFromIsotopologue[1] = experimentalMassToChargeOfIsotopologue;
                            trainingPointFromIsotopologue[2] = retentionTimeOfIsotopologue;
                            consideredIsotopologueTrainingPoints.Add(trainingPointFromIsotopologue);
                            consideredIsotopologueLabels.Add(experimentalMassToChargeOfIsotopologue - calculatedMassToChargeOfIsotopologue);

                        }
                    }

                    if (numIdentifiedIsotopologues < numIsotopologuesNeededToBeConsideredIdentified)
                        break;
                    else
                    {
                        consideredChargeTrainingPoints.AddRange(consideredIsotopologueTrainingPoints);
                        consideredChargeLabels.AddRange(consideredIsotopologueLabels);
                        numIdentifiedCharges++;
                    }
                }
                if (numIdentifiedCharges < numChargesNeededToBeConsideredIdentified) {
                    continue;
                }
                else
                {
                    trainingData.AddRange(consideredChargeTrainingPoints);
                    labelData.AddRange(consideredChargeLabels);
                }
                
            }

            // Create the calibration function

            WriteTrainingDataToFiles(trainingData, labelData);


            //CalibrationFunction cf = new CalibrationFunction(trainingData, labelData);
            //var cf = new CalibrationFunctionHack(trainingData, labelData);
            var cf = new CalibrationFunctionHackMean(trainingData, labelData);

            List<CalibratedSpectrum> calibratedSpectra = new List<CalibratedSpectrum>();
            for (int i = 0; i < numToCreate; i++)
            {
                var s = myMSDataFile[i + 1];
                calibratedSpectra.Add(new CalibratedSpectrum());
                var mzValues = s.MassSpectrum.GetMasses();
                for (int j = 0; j < s.MassSpectrum.Count; j++)
                    mzValues[j] -= cf.calibrate(mzValues[j], s.RetentionTime);
                calibratedSpectra[i].AddMZValues(mzValues);
            }

            return calibratedSpectra;
        }

        private static string ConvertToCSMSLFormula(string theFormula)
        {
            theFormula = Regex.Replace(theFormula, @"[\s()]", "");
            return theFormula;
        }

        private static void WriteTrainingDataToFiles(List<double[]> trainingData, List<double> labelData)
        {
            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"E:\Stefan\data\CalibratedOutput\trainingData1.dat"))
            {
                foreach (double[] d in trainingData)
                {
                    file.WriteLine(d[1]);
                }
            }

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"E:\Stefan\data\CalibratedOutput\trainingData2.dat"))
            {
                foreach (double[] d in trainingData)
                {
                    file.WriteLine(d[2]);
                }
            }

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"E:\Stefan\data\CalibratedOutput\labelData.dat"))
            {
                foreach (double d in labelData)
                {
                    file.WriteLine(d);
                }
            }
        }

        private static int GetSpectrumIndexFromSpectrumID(string spectrumID)
        {
            return Convert.ToInt32(Regex.Match(spectrumID, @"\d+$").Value);
        }
        private static int GetLastNumberFromString(string s)
        {
            return Convert.ToInt32(Regex.Match(s, @"\d+$").Value);
        }

        private static indexedmzML GetMyIndexedMZml(IMSDataFile<ISpectrum<IPeak>> myMSDataFile, List<CalibratedSpectrum> calibratedSpectra, int numToCreate)
        {
            indexedmzML _indexedmzMLConnection = new indexedmzML();
            _indexedmzMLConnection.mzML = new mzMLType();
            _indexedmzMLConnection.mzML.version = "1";

            _indexedmzMLConnection.mzML.cvList = new CVListType();
            _indexedmzMLConnection.mzML.cvList.count = "1";
            _indexedmzMLConnection.mzML.cvList.cv = new CVType[1];
            _indexedmzMLConnection.mzML.cvList.cv[0] = new CVType();
            _indexedmzMLConnection.mzML.cvList.cv[0].URI = @"https://raw.githubusercontent.com/HUPO-PSI/psi-ms-CV/master/psi-ms.obo";
            _indexedmzMLConnection.mzML.cvList.cv[0].fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
            _indexedmzMLConnection.mzML.cvList.cv[0].id = "MS";

            _indexedmzMLConnection.mzML.fileDescription = new FileDescriptionType();
            _indexedmzMLConnection.mzML.fileDescription.fileContent = new ParamGroupType();
            _indexedmzMLConnection.mzML.fileDescription.fileContent.cvParam = new CVParamType[2];
            _indexedmzMLConnection.mzML.fileDescription.fileContent.cvParam[0] = new CVParamType();
            _indexedmzMLConnection.mzML.fileDescription.fileContent.cvParam[0].accession = "MS:1000579"; // MS1 Data
            _indexedmzMLConnection.mzML.fileDescription.fileContent.cvParam[1] = new CVParamType();
            _indexedmzMLConnection.mzML.fileDescription.fileContent.cvParam[1].accession = "MS:1000580"; // MSn Data

            _indexedmzMLConnection.mzML.softwareList = new SoftwareListType();
            _indexedmzMLConnection.mzML.softwareList.count = "1";

            _indexedmzMLConnection.mzML.softwareList.software = new SoftwareType[1];
            // For a RAW file!!!
            // ToDo: read softwareList from mzML file
            //_indexedmzMLConnection.mzML.softwareList.software[1] = new SoftwareType();
            //_indexedmzMLConnection.mzML.softwareList.software[1].id = "ThermoSoftware";
            //_indexedmzMLConnection.mzML.softwareList.software[1].version = rawFile.GetSofwareVersion();
            //_indexedmzMLConnection.mzML.softwareList.software[1].cvParam = new CVParamType[1];
            //_indexedmzMLConnection.mzML.softwareList.software[1].cvParam[0] = new CVParamType();
            //_indexedmzMLConnection.mzML.softwareList.software[1].cvParam[0].accession = "MS:1000693";

            _indexedmzMLConnection.mzML.softwareList.software[0] = new SoftwareType();
            _indexedmzMLConnection.mzML.softwareList.software[0].id = "StefanSoftware";
            _indexedmzMLConnection.mzML.softwareList.software[0].version = "1";
            _indexedmzMLConnection.mzML.softwareList.software[0].cvParam = new CVParamType[1];
            _indexedmzMLConnection.mzML.softwareList.software[0].cvParam[0] = new CVParamType();
            _indexedmzMLConnection.mzML.softwareList.software[0].cvParam[0].accession = "MS:1000799";
            _indexedmzMLConnection.mzML.softwareList.software[0].cvParam[0].value = "StefanSoftware";


            // Leaving empty. Can't figure out the configurations. 
            // ToDo: read instrumentConfigurationList from mzML file
            _indexedmzMLConnection.mzML.instrumentConfigurationList = new InstrumentConfigurationListType();

            _indexedmzMLConnection.mzML.dataProcessingList = new DataProcessingListType();
            // Only writing mine! Might have had some other data processing (but not if it is a raw file)
            // ToDo: read dataProcessingList from mzML file
            _indexedmzMLConnection.mzML.dataProcessingList.count = "1";
            _indexedmzMLConnection.mzML.dataProcessingList.dataProcessing = new DataProcessingType[1];
            _indexedmzMLConnection.mzML.dataProcessingList.dataProcessing[0] = new DataProcessingType();
            _indexedmzMLConnection.mzML.dataProcessingList.dataProcessing[0].id = "StefanDataProcessing";


            _indexedmzMLConnection.mzML.run = new RunType();

            // ToDo: Finish the chromatogram writing!
            _indexedmzMLConnection.mzML.run.chromatogramList = new ChromatogramListType();
            _indexedmzMLConnection.mzML.run.chromatogramList.count = "1";
            _indexedmzMLConnection.mzML.run.chromatogramList.chromatogram = new CSMSL.IO.MzML.ChromatogramType[1];
            _indexedmzMLConnection.mzML.run.chromatogramList.chromatogram[0] = new CSMSL.IO.MzML.ChromatogramType();

            _indexedmzMLConnection.mzML.run.spectrumList = new SpectrumListType();
            _indexedmzMLConnection.mzML.run.spectrumList.count = (numToCreate).ToString();
            _indexedmzMLConnection.mzML.run.spectrumList.defaultDataProcessingRef = "StefanDataProcessing";
            _indexedmzMLConnection.mzML.run.spectrumList.spectrum = new SpectrumType[numToCreate];

            // Loop over all spectra
            for (int i = 0; i < numToCreate; i++)
            {
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i] = new SpectrumType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].defaultArrayLength = myMSDataFile[i + 1].MassSpectrum.Count;
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].index = i.ToString();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].id = myMSDataFile[i + 1].id;

                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam = new CVParamType[7];

                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[0] = new CVParamType();

                if (myMSDataFile[i + 1].MsnOrder == 1)
                {
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[0].accession = "MS:1000579";
                }
                else if (myMSDataFile[i + 1].MsnOrder == 2)
                {
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[0].accession = "MS:1000580";

                    // So needs a precursor!
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList = new PrecursorListType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.count = 1.ToString();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor = new PrecursorType[1];
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0] = new PrecursorType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].spectrumRef = myMSDataFile[i + 1].PrecursorID;
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList = new SelectedIonListType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.count = 1.ToString();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon = new ParamGroupType[1];
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0] = new ParamGroupType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam = new CVParamType[3];
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[0] = new CVParamType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[0].name = "selected ion m/z";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[0].value = myMSDataFile[i + 1].SelectedIonMonoisotopicMZ.ToString();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[0].accession = "MS:1000744";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[1] = new CVParamType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[1].name = "charge state";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[1].value = myMSDataFile[i + 1].SelectedIonChargeState.ToString();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[1].accession = "MS:1000041";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[2] = new CVParamType();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[2].name = "peak intensity";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[2].value = myMSDataFile[i + 1].SelectedIonIsolationIntensity.ToString();
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].precursorList.precursor[0].selectedIonList.selectedIon[0].cvParam[2].accession = "MS:1000042";



                }

                // OPTIONAL, but need for CSMSL reader. ms level
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[1] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[1].name = "ms level";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[1].accession = "MS:1000511";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[1].value = myMSDataFile[i + 1].MsnOrder.ToString();

                // Centroid?
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[2] = new CVParamType();
                if (myMSDataFile[i + 1].isCentroid)
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[2].accession = "MS:1000127";
                else
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[2].accession = "MS:1000128";

                // Polarity
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[3] = new CVParamType();
                if (myMSDataFile[i + 1].Polarity == CSMSL.Polarity.Negative)
                {
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[3].name = "negative scan";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[3].accession = "MS:1000129";
                }
                else if (myMSDataFile[i + 1].Polarity == CSMSL.Polarity.Positive)
                {
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[3].name = "positive scan";
                    _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[3].accession = "MS:1000130";
                }

                // Spectrum title
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[4] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[4].name = "spectrum title";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[4].accession = "MS:1000796";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[4].value = myMSDataFile[i + 1].id;


                // Lowest observed mz
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[5] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[5].name = "lowest observed m/z";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[5].accession = "MS:1000528";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[5].value = myMSDataFile[i + 1].MassSpectrum.FirstMZ.ToString();


                // Highest observed mz
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[6] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[6].name = "highest observed m/z";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[6].accession = "MS:1000527";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].cvParam[6].value = myMSDataFile[i + 1].MassSpectrum.LastMZ.ToString();



                // Retention time
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList = new ScanListType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.count = "1";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan = new ScanType[1];
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0] = new ScanType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam = new CVParamType[1];
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].name = "scan start time";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].accession = "MS:1000016";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].value = myMSDataFile[i + 1].RetentionTime.ToString();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].unitCvRef = "UO";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].unitAccession = "UO:0000031";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].scanList.scan[0].cvParam[0].unitName = "minute";

                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList = new BinaryDataArrayListType();

                // ONLY WRITING M/Z AND INTENSITY DATA, NOT THE CHARGE! (but can add charge info later)
                // CHARGE (and other stuff) CAN BE IMPORTANT IN ML APPLICATIONS!!!!!
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.count = 2.ToString();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray = new BinaryDataArrayType[2];

                // M/Z Data
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0] = new BinaryDataArrayType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].binary = Mzml.ConvertDoublestoBase64(calibratedSpectra[i].mzValues, false, false);
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam = new CVParamType[2];
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0].accession = "MS:1000514";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0].name = "m/z array";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[1] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[1].accession = "MS:1000523";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[1].name = "64-bit float";
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0] = new CVParamType();
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0].accession = "MS:1000574";
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[0].cvParam[0].name = "zlib compression";

                // Intensity Data
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1] = new BinaryDataArrayType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].binary = Mzml.ConvertDoublestoBase64(myMSDataFile[i + 1].MassSpectrum.GetIntensities(), false, false);
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam = new CVParamType[2];
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0].accession = "MS:1000515";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0].name = "intensity array";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[1] = new CVParamType();
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[1].accession = "MS:1000523";
                _indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[1].name = "64-bit float";
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0] = new CVParamType();
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0].accession = "MS:1000574";
                //_indexedmzMLConnection.mzML.run.spectrumList.spectrum[i].binaryDataArrayList.binaryDataArray[1].cvParam[0].name = "zlib compression";
            }


            return _indexedmzMLConnection;
        }
    }
}
