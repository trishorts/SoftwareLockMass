﻿using IO.MzML;
using IO.Thermo;
using MassSpectrometry;
using Proteomics;
using SoftwareLockMass;
using Spectra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SoftwareLockMassIO
{
    public static class IO
    {
        public static UsefulProteomicsDatabases.Generated.unimod unimodDeserialized;
        public static UsefulProteomicsDatabases.Generated.obo psimodDeserialized;
        public static Dictionary<int, ChemicalFormulaModification> uniprotDeseralized;

        public static string unimodLocation = @"unimod_tables.xml";
        public static string psimodLocation = @"PSI-MOD.obo.xml";
        public static string elementsLocation = @"elements.dat";
        public static string uniprotLocation = @"ptmlist.txt";

        private static int GetLastNumberFromString(string s)
        {
            return Convert.ToInt32(Regex.Match(s, @"\d+$").Value);
        }

        public static SoftwareLockMassParams GetReady(string origDataFile, EventHandler<OutputHandlerEventArgs> p_outputHandler, EventHandler<ProgressHandlerEventArgs> p_progressHandler, EventHandler<OutputHandlerEventArgs> p_watchHandler, string mzidFile)
        {
            IMsDataFile<IMzSpectrum<MzPeak>> myMsDataFile;
            if (Path.GetExtension(origDataFile).Equals(".mzML"))
                myMsDataFile = new Mzml(origDataFile);
            else
                myMsDataFile = new ThermoRawFile(origDataFile);
            var a = new SoftwareLockMassParams(myMsDataFile);
            a.outputHandler += p_outputHandler;
            a.progressHandler += p_progressHandler;
            a.watchHandler += p_watchHandler;
            a.postProcessing = MzmlOutput;
            a.getFormulaFromDictionary = getFormulaFromDictionary;
            a.identifications = new MzidIdentifications(mzidFile);

            //a.MS1spectraToWatch.Add(11278);
            //a.mzRange = new DoubleRange(1139, 1142);

            //a.MS2spectraToWatch.Add(2);

            //a.MS2spectraToWatch.Add(11279);
            //a.MS2spectraToWatch.Add(2813);
            //a.MS2spectraToWatch.Add(11277);
            //a.MS2spectraToWatch.Add(11290);
            //a.MS2spectraToWatch.Add(2806);
            //a.MS2spectraToWatch.Add(11357);
            //a.MS2spectraToWatch.Add(11296);
            //a.MS2spectraToWatch.Add(11359);
            //a.MS2spectraToWatch.Add(11188);
            //a.MS2spectraToWatch.Add(5669);
            //a.MS2spectraToWatch.Add(11324);
            //a.MS2spectraToWatch.Add(11285);
            //a.MS2spectraToWatch.Add(11283);
            //a.MS2spectraToWatch.Add(3181);
            //a.MS2spectraToWatch.Add(4047);
            //a.MS2spectraToWatch.Add(4053);
            //a.MS2spectraToWatch.Add(5388);
            //a.MS2spectraToWatch.Add(3194);
            //a.MS2spectraToWatch.Add(3766);
            //a.MS2spectraToWatch.Add(3842);
            //a.MS2spectraToWatch.Add(3849);
            //a.MS2spectraToWatch.Add(11210);
            //a.MS2spectraToWatch.Add(5894);

            return a;
        }

        public static void Load()
        {

            UsefulProteomicsDatabases.Loaders.LoadElements(elementsLocation);
            unimodDeserialized = UsefulProteomicsDatabases.Loaders.LoadUnimod(unimodLocation);
            psimodDeserialized = UsefulProteomicsDatabases.Loaders.LoadPsiMod(psimodLocation);
            uniprotDeseralized = UsefulProteomicsDatabases.Loaders.LoadUniprot(uniprotLocation);
        }

        public static string getFormulaFromDictionary(string dictionary, string acession)
        {
            if (dictionary == "UNIMOD")
            {
                string unimodAcession = acession;
                var indexToLookFor = GetLastNumberFromString(unimodAcession) - 1;
                while (unimodDeserialized.modifications[indexToLookFor].record_id != GetLastNumberFromString(unimodAcession))
                    indexToLookFor--;
                return Regex.Replace(unimodDeserialized.modifications[indexToLookFor].composition, @"[\s()]", ""); ;
            }
            else if (dictionary == "PSI-MOD")
            {
                string psimodAcession = acession;
                UsefulProteomicsDatabases.Generated.oboTerm ksadklfj = (UsefulProteomicsDatabases.Generated.oboTerm)psimodDeserialized.Items[GetLastNumberFromString(psimodAcession) + 2];

                if (GetLastNumberFromString(psimodAcession) != GetLastNumberFromString(ksadklfj.id))
                    throw new Exception("Error in reading psi-mod file, acession mismatch!");
                else
                {
                    foreach (var a in ksadklfj.xref_analog)
                    {
                        if (a.dbname == "DiffFormula")
                        {
                            return Regex.Replace(a.name, @"[\s()]", "");
                        }
                    }
                    return uniprotDeseralized[GetLastNumberFromString(psimodAcession)].ThisChemicalFormula.Formula;
                }
            }
            else
                throw new Exception("Not familiar with modification dictionary " + dictionary);
        }

        public static void MzmlOutput(SoftwareLockMassParams p, List<IMzSpectrum<MzPeak>> calibratedSpectra, List<double> calibratedPrecursorMZs)
        {
            p.OnOutput(new OutputHandlerEventArgs("Creating _indexedmzMLConnection, and putting data in it"));
            MzmlMethods.CreateAndWriteMyIndexedMZmlwithCalibratedSpectra(p.myMsDataFile, calibratedSpectra, calibratedPrecursorMZs, Path.Combine(Path.GetDirectoryName(p.myMsDataFile.FilePath), Path.GetFileNameWithoutExtension(p.myMsDataFile.FilePath) + "-Calibrated.mzML"));
        }

    }
}
