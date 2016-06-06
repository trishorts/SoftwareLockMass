﻿using IO.MzML;
using IO.Thermo;
using SoftwareLockMass;
using Spectra;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using UsefulProteomicsDatabases;

namespace SoftwareLockMassGUI
{
    public partial class Form1 : Form
    {
        public static string unimodLocation = @"unimod_tables.xml";
        public static string psimodLocation = @"PSI-MOD.obo.xml";
        public static string elementsLocation = @"elements.dat";

        public static List<AnEntry> myListOfEntries;

        private BindingList<AnEntry> binding1;

        public Form1()
        {
            InitializeComponent();
            Loaders.unimodLocation = unimodLocation;
            Loaders.psimodLocation = psimodLocation;
            Loaders.elementLocation = elementsLocation;
            Loaders.LoadElements();

            myListOfEntries = new List<AnEntry>();
            //myListOfEntries.Add(new AnEntry("some raw file", "some mzid file"));
            //myListOfEntries.Add(new AnEntry("some mzml file", "corresponding mzid file"));
            
            binding1 = new BindingList<AnEntry>(myListOfEntries); // <-- BindingList

            dataGridView1.DataSource = binding1;
            

            // THIS IS JUST FOR DEBUGGING   
            //origDataFile = @"E:\Stefan\data\jurkat\120426_Jurkat_highLC_Frac1.raw";
            //mzidFile = @"E:\Stefan\data\4FileExperiments\4FileExperiment10ppmForCalibration\120426_Jurkat_highLC_Frac1.mzid";

            //SoftwareLockMassRunner.p = new SoftwareLockMassParams(origDataFile, mzidFile);
            //SoftwareLockMassRunner.p.outputHandler += P_outputHandler;
            //SoftwareLockMassRunner.p.progressHandler += P_progressHandler;
            //SoftwareLockMassRunner.p.watchHandler += P_watchHandler;

            //SoftwareLockMassRunner.p.MS1spectraToWatch.Add(11187);
            //SoftwareLockMassRunner.p.MS2spectraToWatch.Add(11188);
            //SoftwareLockMassRunner.p.mzRange = new Range<double>(1113.4,1114.5);

            //SoftwareLockMassRunner.p.MS1spectraToWatch.Add(11289);
            //SoftwareLockMassRunner.p.MS2spectraToWatch.Add(11290);
            //SoftwareLockMassRunner.p.mzRange = new Range<double>(1163, 1167);

            //SoftwareLockMassRunner.p.MS1spectraToWatch.Add(5893);
            //SoftwareLockMassRunner.p.MS2spectraToWatch.Add(5894);
            //SoftwareLockMassRunner.p.mzRange = new Range<double>(948,952);

            //Thread thread = new Thread(new ThreadStart(SoftwareLockMassRunner.Run));
            //thread.IsBackground = true;
            //thread.Start();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.Filter = "Mass Spec Files(*.raw;*.mzML;*.mzid)|*.raw;*.mzML;*.mzid|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                addFilePaths(openFileDialog1.FileNames);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SoftwareLockMassRunner.p = new SoftwareLockMassParams(myListOfEntries);
            SoftwareLockMassRunner.p.outputHandler += P_outputHandler;
            SoftwareLockMassRunner.p.progressHandler += P_progressHandler;
            SoftwareLockMassRunner.p.watchHandler += P_watchHandler;
            
            Thread thread = new Thread(new ThreadStart(SoftwareLockMassRunner.Run));
            thread.IsBackground = true;
            thread.Start();

        }

        private void P_watchHandler(object sender, OutputHandlerEventArgs e)
        {
            if (textBox2.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(P_watchHandler);
                Invoke(d, new object[] { sender, e });
            }
            else
            {
                textBox2.AppendText(e.output + "\n");
            }
        }

        private void P_progressHandler(object sender, ProgressHandlerEventArgs e)
        {
            if (progressBar1.InvokeRequired)
            {
                SetProgressCallback d = new SetProgressCallback(P_progressHandler);
                Invoke(d, new object[] { sender, e });
            }
            else
            {
                progressBar1.Value = Math.Min(e.progress,100);
            }
        }

        private void P_outputHandler(object sender, OutputHandlerEventArgs e)
        {
            if (textBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(P_outputHandler);
                Invoke(d, new object[] { sender,  e });
            }
            else
            {
                textBox1.AppendText(e.output + "\n");
            }
        }

        delegate void SetTextCallback(object sender, OutputHandlerEventArgs e);
        delegate void SetProgressCallback(object sender, ProgressHandlerEventArgs e);
        
        private void addFilePaths(string[] filepaths)
        {
            foreach (string filepath in filepaths)
            {
                Console.WriteLine(filepath);
                var theExtension = Path.GetExtension(filepath);
                var pathNoExtension = Path.GetFileNameWithoutExtension(filepath);
                var foundOne = false;
                foreach (AnEntry a in myListOfEntries)
                {
                    if (theExtension.Equals(".raw") || theExtension.Equals(".mzml"))
                    {
                        if (a.mzidFile != null && Path.GetFileNameWithoutExtension(a.mzidFile).Equals(pathNoExtension))
                        {
                            a.spectraFile = filepath;
                            foundOne = true;
                            dataGridView1.Refresh();
                            dataGridView1.Update();
                            break;
                        }
                    }
                    if (theExtension.Equals(".mzid"))
                    {
                        Console.WriteLine(Path.GetFileNameWithoutExtension(a.spectraFile));
                        Console.WriteLine(pathNoExtension);
                        if (a.spectraFile != null && Path.GetFileNameWithoutExtension(a.spectraFile).Equals(pathNoExtension))
                        {
                            a.mzidFile = filepath;
                            foundOne = true;
                            dataGridView1.Refresh();
                            dataGridView1.Update();
                            break;
                        }
                    }
                }
                if (!foundOne)
                {
                    Console.WriteLine("Adding " + filepath);
                    Console.WriteLine("extension " + theExtension);
                    if (theExtension.Equals(".raw") || theExtension.Equals(".mzml"))
                    {
                        Console.WriteLine("raw or mzml ");
                        binding1.Add(new AnEntry(filepath, null));
                    }
                    if (theExtension.Equals(".mzid"))
                    {
                        Console.WriteLine("mzid ");
                        binding1.Add(new AnEntry(null, filepath));
                    }
                }
            }
        }
        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] filepaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            addFilePaths(filepaths);
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Link;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            binding1.Clear();
        }
    }
}
