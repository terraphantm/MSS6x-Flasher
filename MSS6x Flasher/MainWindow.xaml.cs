using System;
using System.Configuration;
//using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using EdiabasLib;

namespace MSS6x_Flasher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }


        public MainWindow()
        {
            InitializeComponent();
        }

        private bool CurrentlyFlashing = false;

        private bool FullbinLoaded = false;

        private static String WildCardToRegular(String value)
        {
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }



        private void Flashing(object sender, EventArgs e)
        {
            CurrentlyFlashing = true;
            this.Dispatcher.Invoke(() =>
            {
            FunctionStack.IsEnabled = false;
            AdvancedMenu.IsEnabled = false;
            });
        }

        private void NotFlashing(object sender, EventArgs e)
        {
            CurrentlyFlashing = false;
            this.Dispatcher.Invoke(() =>
            {
                FunctionStack.IsEnabled = true;
                AdvancedMenu.IsEnabled = true;
            });
        }


        private void FullBinIsLoaded(object sender, EventArgs e)
        {
            FullbinLoaded = true;
        }

        private void FullBinNotLoaded(object sender, EventArgs e)
        {
            FullbinLoaded = false;
        }

        public bool IsFullBinLoaded()
        {
            return FullbinLoaded;
        }

        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.CurrentlyFlashing)
            {
                string msg = "Currently flashing DME, exiting now may have unpredictable results.\n\nAre you sure you want to exit?";
                MessageBoxResult result =
                  MessageBox.Show(
                    msg,
                    "Currently Flashing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    // If user doesn't want to close, cancel closure
                    e.Cancel = true;
                }
            }
        }


        private void IdentDME()
        {

            string DMEType;
            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {


                ExecuteJob(ediabas, "aif_lesen", string.Empty);

                Global.VIN = GetResult_String("AIF_FG_NR", ediabas.ResultSets);

                ExecuteJob(ediabas, "hardware_referenz_lesen", string.Empty);

                Global.HW_Ref = GetResult_String("HARDWARE_REFERENZ", ediabas.ResultSets);

                ExecuteJob(ediabas, "daten_referenz_lesen", string.Empty);

                String SW_Ref = GetResult_String("DATEN_REFERENZ", ediabas.ResultSets);
                if (SW_Ref.Length > 12)
                    SW_Ref = SW_Ref.Substring(12);

                ExecuteJob(ediabas, "zif_lesen", string.Empty);
                string zif = string.Empty;
                if (GetResult_String("ZIF_PROGRAMM_REFERENZ", ediabas.ResultSets).Contains(Global.HW_Ref))
                    zif = GetResult_String("ZIF_PROGRAMM_STAND", ediabas.ResultSets);
                else
                {
                    ExecuteJob(ediabas, "zif_backup_lesen", string.Empty);
                    if (GetResult_String("ZIF_BACKUP_PROGRAMM_REFERENZ", ediabas.ResultSets).Contains(Global.HW_Ref))
                        zif = GetResult_String("ZIF_BACKUP_PROGRAMM_STAND", ediabas.ResultSets);
                }

                Global.ZIF = zif;
                ExecuteJob(ediabas, "flash_programmier_status_lesen", string.Empty);

                string programming_status = GetResult_String("FLASH_PROGRAMMIER_STATUS_TEXT", ediabas.ResultSets);

                DMEType = "Unknown / Unsuppported";

                if (Global.HW_Ref == "0569Q60")
                    DMEType = "MSS65";
                if (Global.HW_Ref == "0569QT0")
                    DMEType = "MSS60";            

               
                this.Dispatcher.Invoke(() =>
                {
                    DMEType_Box.Content = DMEType;
                    HWRef_Box.Content = Global.HW_Ref;
                    ZIF_Box.Content = Global.ZIF;
                    SWRef_Box.Content = SW_Ref;
                    programStatus_Box.Content = programming_status;
                    VIN_Box.Content = Global.VIN;
                    //ReadFull.IsEnabled = true;
                    //AdvancedMenu.IsEnabled = true;


                    if (DMEType != String.Empty && DMEType != "Unknown / Unsuppported")
                    {
                        AdvancedMenu.IsEnabled = true;
                        ReadFull.IsEnabled = true;
                        ReadTune.IsEnabled = true;
                        LoadFile.IsEnabled = true;
                    }
                });
            }
        }

        private void LoadFile_1()
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.InitialDirectory = System.IO.Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            openFile.Filter = "Binary|*.bin|Original File|*.ori|All Files|*.*";
            Nullable<bool> result = openFile.ShowDialog();
            Global.openedFlash = null;
            byte[] file = new byte[0];
            if (result == true)
            {
                file = File.ReadAllBytes(openFile.FileName);
                if (file.Length == 0x20000)
                {
                    int i = 0;
                    if (VerifyParameterMatch(file, Global.ZIF))
                    {
                        Global.openedFlash = file;
                        FullBinNotLoaded(this, null);
                        FlashDME.IsEnabled = true;
                        FlashProgram.IsEnabled = false;
                        RSA_Bypass_Fast.IsEnabled = false;
                        //RSA_Bypass_Slow.IsEnabled = false;


                    }
                    else
                    {
                        FlashDME.IsEnabled = false;
                        FlashProgram.IsEnabled = false;
                        RSA_Bypass_Fast.IsEnabled = false;
                        //RSA_Bypass_Slow.IsEnabled = false;
                    }
                }
                else if (file.Length == 0x500000)
                {
                    if (VerifyProgramMatch(file))
                    {
                        Global.openedFlash = file;
                        FullBinIsLoaded(this, null);
                        FlashDME.IsEnabled = true;
                        FlashProgram.IsEnabled = true;
                        RSA_Bypass_Fast.IsEnabled = true;
                        //RSA_Bypass_Slow.IsEnabled = true;
                    }
                    else
                    {
                        FlashDME.IsEnabled = false;
                        FlashProgram.IsEnabled = false;
                        RSA_Bypass_Fast.IsEnabled = false;
                        //RSA_Bypass_Slow.IsEnabled = false;
                    }
                }
                else
                {
                    FlashDME.IsEnabled = false;
                    FlashProgram.IsEnabled = false;
                    RSA_Bypass_Fast.IsEnabled = false;
                    //RSA_Bypass_Slow.IsEnabled = false;
                }
                file = null;
                
            }

            else
            {
                FlashDME.IsEnabled = false;
                Global.openedFlash = null;
                file = null;
            }

            if (Global.openedFlash != null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Loaded " + Path.GetFileName(openFile.FileName);
                    //Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();

                    //ChecksumsSignatures.BypassRSA(Global.openedFlash.Skip(0x230000).Take(0x230000).ToArray());
                    //ChecksumsSignatures.CorrectProgramChecksums(Global.openedFlash.Take(0x230000).ToArray());
                    //ChecksumsSignatures.BypassRSA(Global.openedFlash.Skip(0x230000).Take(0x230000).ToArray());
                    //ChecksumsSignatures.CorrectProgramChecksums(ChecksumsSignatures.BypassRSA(Global.openedFlash.Skip(0x230000).Take(0x230000).ToArray()));
                    //ChecksumsSignatures.CorrectProgramChecksums(Global.openedFlash.Skip(0x230000).Take(0x230000).ToArray());
                    
                    //ChecksumsSignatures.CorrectParameterChecksums(Global.openedFlash.Skip(0x70000).Take(0x10000).ToArray());
                    //ChecksumsSignatures.CorrectParameterChecksums(Global.openedFlash.Skip(0x230000 + 0x70000).Take(0x10000).ToArray());
                    //Console.WriteLine(ChecksumsSignatures.CalculateChecksum(Global.openedFlash.Skip(0x0000).Take(0x10000).ToArray(), 0x1C0, 0xFFFFFFFF, 0x70000, 0x7FFFB).ToString("x"));

                });
            }
        }

        private bool VerifyParameterMatch(byte[] flash, string zif)
        {
            bool match = false;
            byte[] binheader1 = flash.Take(0x8).ToArray();
            byte[] binheader2 = flash.Skip(0x10000).Take(0x8).ToArray();
            byte[] binheadercompare = { 0x5A, 0x5A, 0x5A, 0x5A, 0xCC, 0xCC, 0xCC, 0xCC };
            Console.WriteLine(BitConverter.ToString(binheader1));
            Console.WriteLine(BitConverter.ToString(binheader2));

            string binref1 = System.Text.Encoding.ASCII.GetString(flash.Skip(0x256).Take(0x37).ToArray());
            string binref2 = System.Text.Encoding.ASCII.GetString(flash.Skip(0x10256).Take(0x37).ToArray());


            if (!binheader1.SequenceEqual(binheadercompare) || !binheader1.SequenceEqual(binheader2))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Not a valid tune";
                });
                return match;
            }
            if (!binref1.Contains(Global.HW_Ref))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Tune does not match hardware";
                });
                return match;
            }

            zif = "*" + zif.Substring(0,2) + "?" + zif.Substring(3) + "*";
            //Console.WriteLine(zif);
            //Console.WriteLine(binref1);
            if (!Regex.IsMatch(binref1, WildCardToRegular(zif)))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Tune does not match program";
                });
                return match;
            }

            if (binref1 != binref2)
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Injection and Ignition tunes do not match";
                });
                return match;
            }


            if (flash[0x252] != 1 && flash[0x10252] != 2)
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Not in Injection / Ignition order";
                });
                return match;
            }
            match = true;
            return match;
        }

        private bool VerifyProgramMatch(byte[] flash)
        {
            bool match = false;

            byte[] binheader1 = flash.Skip(0x10000).Take(0x8).ToArray();
            byte[] binheader2 = flash.Skip(0x290000).Take(0x8).ToArray();
            byte[] binheadercompare = { 0x5A, 0x5A, 0x5A, 0x5A, 0x33, 0x33, 0x33, 0x33 };

            uint flashExtEnd_Inj = BitConverter.ToUInt32(flash.Skip(0x1001c).Take(4).Reverse().ToArray(), 0);
            uint flashExtEnd_Ign = BitConverter.ToUInt32(flash.Skip(0x29001c).Take(4).Reverse().ToArray(), 0);



            if (flashExtEnd_Inj > 0x4FFFFF)
                flashExtEnd_Inj = 0x4FFFFF;
            if (flashExtEnd_Ign > 0x4FFFFF)
                flashExtEnd_Ign = 0x4FFFFF;

            byte[] flashfooter1 = flash.Skip((int)(flashExtEnd_Inj - 0x380000)).Take(4).ToArray();
            byte[] flashfooter2 = flash.Skip((int)(flashExtEnd_Ign - 0x100000)).Take(4).ToArray();
            
            string binref1 = System.Text.Encoding.ASCII.GetString(flash.Skip(0x10248).Take(0x24).ToArray());
            string binref2 = System.Text.Encoding.ASCII.GetString(flash.Skip(0x290248).Take(0x24).ToArray());

            if (!binheader1.SequenceEqual(binheadercompare) || !binheader1.SequenceEqual(binheader2))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Not a valid program";
                });
                return match;
            }

            if (!flashfooter1.SequenceEqual(flashfooter2) || !flashfooter1.SequenceEqual(binheadercompare.Take(4).ToArray()))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "External flash not valid";
                });
                return match;
            }

            if (!binref1.Contains(Global.HW_Ref))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Program does not match hardware";
                });
            return match;
            }
            if (binref1 != binref2)
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Injection and Ignition programs do not match";
                });
                return match;
            }
            string zif = binref1.Substring(8, 4);

            match = VerifyParameterMatch(flash.Skip(0x70000).Take(0x10000).Concat(flash.Skip(0x70000 + 0x280000).Take(0x10000)).ToArray(), zif);

            return match;
        }

        private void UpdateProgressBar(uint progress)
        {
            ProgressDME.Dispatcher.Invoke(() => ProgressDME.Value = progress, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ProgressDME_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressDME.Value = Math.Min(e.ProgressPercentage, 100);
        }




        //Read, Write, Erase

        private async Task rsabypasstasks(bool fastRSABypass)
        {
            byte[] full = Global.openedFlash;
            string zif = Global.ZIF;
            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            bool IsSigValid = ChecksumsSignatures.IsProgramSignatureValid(full);

            MessageBoxResult result;
            String msg = string.Empty;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {

                if (!IsSigValid)
                {
                    msg = "The file you loaded is not a stock binary. The RSA bypass requires a stock binary be used. Please reload the appropriate file and try again.";
                    result = MessageBox.Show(msg, "Non-stock binary",  MessageBoxButton.OK,  MessageBoxImage.Error);
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "RSA Bypass Cancelled";
                    });

                    return;

                }


                //zif = "*" + zif.Substring(0, 2) + "?" + zif.Substring(3) + "*";
                //Console.WriteLine(zif);
                //Console.WriteLine(binref1);

                string progRef_FromBinary = System.Text.Encoding.ASCII.GetString(full.Skip(0x10248).Take(0x24).ToArray());
                string progRef_FromDME = System.Text.Encoding.ASCII.GetString(ReadMemory(ediabas, 0x10248, 0x10248 + 0x24 - 1, String.Empty));

                if (!(progRef_FromBinary == progRef_FromDME) && fastRSABypass)
                {
                    msg = "The file you loaded is not the same as what is installed on the DME. The RSA bypass routine requires you use the same program as currently on the DME.";
                    result = MessageBox.Show(msg,  "Loaded program does not match installed software", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "RSA Bypass Cancelled";
                    });

                    return;
                }

                if (!(progRef_FromBinary == progRef_FromDME) && !fastRSABypass)
                {
                    msg = "The file you loaded is not the same as what is installed on the DME or the DME version could not be read. The RSA bypass routine requires you use the same program as currently on the DME. Try flashing anyway?";
                    result = MessageBox.Show(msg, "Loaded program does not match installed software", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "RSA Bypass Cancelled";
                        });

                        return;
                    }
                }
            }


            msg = "Warning: If you are not using a cable with the EdiabasLib D-CAN firmware, performing this operation will destroy your DME. " +
                    "If you are unsure what firmware you have, please cancel now.\n\nWould you like to proceed?";


            result = MessageBox.Show(msg, "RSA Bypass", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
                return;

            bool success = true;
            await Task.Run(() => success = Flashfull(full, true, fastRSABypass).Result);
            if (success)
            {
                await Task.Delay(5000);//Probably don't need to wait 5 seconds
                await Task.Run(() => success = FlashDME_Data(full.Skip(0x70000).Take(0x10000).Concat(full.Skip(0x2F0000).Take(0x10000)).ToArray(), true).Result);
            }
            if (success)
            {
                await Task.Delay(5000);
                await Task.Run(() => success = Flashfull(full, false, false).Result);
            }

            if (!success)
                statusTextBlock.Text = "RSA Bypass Failed, aborting...";

            return;

        }

        private async Task ReadRAM() 

        {

            uint start = 0x3F8000;
            uint end = 0x3fffff;
            byte[] Injection = new byte[0];
            byte[] Ignition = new byte[0];
            string ReadingText = String.Empty;


            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {

                ReadingText = "Reading Injection RAM";
                await Task.Run(() => Injection = ReadMemory(ediabas, start, end, ReadingText));

                start += 0x800000;
                end += 0x800000;

                ReadingText = "Reading Ignition RAM";
                await Task.Run(() => Ignition = ReadMemory(ediabas, start, end, ReadingText));
                
              

                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = null;
                });
                // SaveFileDialog saveFile = new SaveFileDialog();
                if (Injection.Length == 0x8000 && Ignition.Length == 0x8000)
                {
                    DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                    File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_" + "Injection_RAM_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Injection);
                    File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_" + "Ignition_RAM_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Ignition);
                    System.Diagnostics.Process.Start(SaveDirectory.ToString());

                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Done reading!\nFile Saved to: " + SaveDirectory.FullName;
                    });
                }

                else
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Something went wrong, please try again";
                    });
                }

            }
        }

        /*private async Task ReadRegisters()

        {

            uint start = 0x2FC000;
            uint end = 0x3fffff;
            byte[] Injection = new byte[0];
            byte[] Ignition = new byte[0];
            string ReadingText = String.Empty;


            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {

                ReadingText = "Reading Injection Registers";
                await Task.Run(() => Injection = ReadMemory(ediabas, start, end, ReadingText));

                start += 0x800000;
                end += 0x800000;

                statusTextBlock.Text = "Reading Ignition Registers";
                await Task.Run(() => Ignition = ReadMemory(ediabas, start, end, ReadingText));



                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = null;
                });
                // SaveFileDialog saveFile = new SaveFileDialog();
                if (Injection.Length == 0x8000 && Ignition.Length == 0x8000)
                {
                    DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                    File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_" + "Injection_RAM_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Injection);
                    File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_" + "Ignition_RAM_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Ignition);
                    System.Diagnostics.Process.Start(SaveDirectory.ToString());

                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Done reading!\nFile Saved to: " + SaveDirectory.FullName;
                    });
                }

                else
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Something went wrong, please try again";
                    });
                }

            }
        }*/

        private async Task ReadDME() //Could probably simplify this by separating full and partial reads.

        {

            uint start = 0;
            uint end = 0;
            byte[] Injection = new byte[0];
            byte[] Ignition = new byte[0];
            string ReadingText = String.Empty;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {


                start = 0x70000;
                end = 0x7FFFF;

                ReadingText = "Reading Injection Tune";
                await Task.Run(() => Injection = ReadMemory(ediabas, start, end, ReadingText));

                start += 0x800000;
                end += 0x800000;

                ReadingText = "Reading Ignition Tune";
                await Task.Run(() => Ignition = ReadMemory(ediabas, start, end, ReadingText));

                byte[] DumpedTune = Injection.Concat(Ignition).ToArray();
                if (DumpedTune.Length == 0x20000)
                {

                    DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                    File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_Tune_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Injection.Concat(Ignition).ToArray());

                    System.Diagnostics.Process.Start(SaveDirectory.ToString());

                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Done reading!\nFile Saved to: " + SaveDirectory.FullName;
                    });
                }
                else
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Something went wrong, please try again";
                    });
                }
            }
        }

        private async Task ReadDME_Full(bool QuickRead)
        {
            uint start = 0;
            uint end = 0;
            byte[] Injection = new byte[0];
            byte[] Ignition = new byte[0];

            byte[] Injection_ext = new byte[0];
            byte[] Ignition_ext = new byte[0];

            byte[] Injection_ext_start = new byte[0];
            byte[] Ignition_ext_start = new byte[0];

            int inj_ext_length = 0;
            int ign_ext_length = 0;

            string ReadingText = String.Empty;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                start = 0x00000;
                end = 0x7FFFF;
                ReadingText = "Reading Injection Internal Flash";
                await Task.Run(() => Injection = ReadMemory(ediabas, start, end, ReadingText));

                if (QuickRead)
                {
                    start = 0x400000;
                    end = 0x407FFF;
                    ReadingText = "Reading Injection Manufacturing data";
                    await Task.Run(() => Injection_ext_start = ReadMemory(ediabas, start, end, ReadingText));
                    Array.Resize(ref Injection_ext_start, 0x30000);
                    for (int i = 0x8000; i < Injection_ext_start.Length; ++i)
                        Injection_ext_start[i] = 0xFF;

                    ReadingText = "Reading Injection External Flash";
                    start = 0x430000;
                    end = BitConverter.ToUInt32(Injection.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7;
                    Console.WriteLine(end);
                    await Task.Run(() => Injection_ext = ReadMemory(ediabas, start, end, ReadingText));
                    Injection_ext = Injection_ext_start.Concat(Injection_ext).ToArray();

                    inj_ext_length = Injection_ext.Length;
                    Array.Resize(ref Injection_ext, 0x200000);
                    for (int i = inj_ext_length; i < 0x200000; ++i)
                        Injection_ext[i] = 0xFF;
                }
                else
                {
                    start = 0x400000;
                    end = 0x5FFFFF;
                    ReadingText = "Reading Injection External Flash";
                    await Task.Run(() => Injection_ext = ReadMemory(ediabas, start, end, ReadingText));
                }




                start = 0x800000;
                end = 0x87FFFF;
                ReadingText = "Reading Ignition Internal Flash";
                await Task.Run(() => Ignition = ReadMemory(ediabas, start, end, ReadingText));


                if (QuickRead)
                {
                    start = 0xC00000;
                    end = 0xC07FFF;
                    ReadingText = "Reading Ignition Manufacturing data";
                    await Task.Run(() => Ignition_ext_start = ReadMemory(ediabas, start, end, ReadingText));
                    Array.Resize(ref Ignition_ext_start, 0x30000);
                    for (int i = 0x8000; i < Ignition_ext_start.Length; ++i)
                        Ignition_ext_start[i] = 0xFF;

                    ReadingText = "Reading Ignition External Flash";
                    start = 0xC30000;
                    end = BitConverter.ToUInt32(Ignition.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7 + 0x800000;
                    Console.WriteLine(end);

                    await Task.Run(() => Ignition_ext = ReadMemory(ediabas, start, end, ReadingText));
                    Ignition_ext = Ignition_ext_start.Concat(Ignition_ext).ToArray();

                    ign_ext_length = Ignition_ext.Length;
                    Array.Resize(ref Ignition_ext, 0x200000);
                    for (int i = ign_ext_length; i < 0x200000; ++i)
                        Ignition_ext[i] = 0xFF;
                }
                else
                {
                    start = 0xC00000;
                    end = 0xDFFFFF;
                    ReadingText = "Reading Ignition External Flash";
                    await Task.Run(() => Ignition_ext = ReadMemory(ediabas, start, end, ReadingText));
                }


                if (Global.HW_Ref == "0569QT0")
                {
                    byte[] EWS4_SK_Header = { 0xA5, 0x00, 0xFF, 0xAA, 0xFF, 0xFF, 0xFF, 0xFF };
                    if (!Injection.Skip(0x7948).Take(0x8).ToArray().SequenceEqual(EWS4_SK_Header))
                    {
                        byte[] InjRAMDump = new byte[0];
                        ReadingText = "Reading RAM";


                        await Task.Run(() => InjRAMDump = ReadMemory(ediabas, 0x3F8000, 0x3FFFFF, ReadingText));

                        int IndexOfSK = FindSK(InjRAMDump);
                        if (IndexOfSK == -1)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Could not find secret key";
                            });
                        }
                        else
                        {
                            byte[] SK = InjRAMDump.Skip(IndexOfSK).Take(0x30).ToArray();
                            byte[] EWS4_SK = EWS4_SK_Header.Concat(SK).ToArray();
                            for (int i = 0; i < 0x38; ++i)
                                Injection[0x7948 + i] = EWS4_SK[i];
                        }
                    }
                }
            }

            byte[] dumpedFlash = Injection.Concat(Injection_ext.Concat(Ignition.Concat(Ignition_ext))).ToArray();
            if (dumpedFlash.Length == 0x500000)
            {
                DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_Full_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", dumpedFlash);
                File.WriteAllBytes(SaveDirectory + @"\" + Global.VIN + "_" + Global.ZIF + "_Tune_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".bin", Injection.Skip(0x70000).Take(0x10000).Concat(Ignition.Skip(0x70000).Take(0x10000)).ToArray());

                System.Diagnostics.Process.Start(SaveDirectory.ToString());

                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Done reading!\nFiles Saved to: " + SaveDirectory.FullName;
                });
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Something went wrong, please try again";
                });
            }

        }

        private int FindSK(byte[] buffer)
        {
            var KeyLength = 0x10;
            var SearchLimit = buffer.Length - (3 * KeyLength);
            for (var i = 0; i < SearchLimit; ++i)
            {
                var k = 0;
                for (; k < KeyLength; k++)
                {
                    if ((buffer[i+k] ^ 0xFF) != buffer[i + k + KeyLength] && (buffer[i+k] ^ 0xAA) != buffer[i+k+KeyLength*2])
                        break;
                }
                if (k == KeyLength)
                    return i;
            }
            return -1;
        }

        private async Task ReadISN_SK()
        {
            {

                uint start = 0x3F8000;
                uint end = 0x3fffff;
                byte[] InjRAMDump = new byte[0];
                byte[] ISN = new byte[0];
                byte[] ProtectedRead = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

                byte[] EWS4_SK_Header = { 0xA5, 0x00, 0xFF, 0xAA, 0xFF, 0xFF, 0xFF, 0xFF };
                byte[] EWS4_SK = new byte[0];



                using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
                {
                    String ReadingText = String.Empty;
                    if (Global.HW_Ref == "0569Q60")
                    {
                        ReadingText = "Reading ISN";
                        await Task.Run(() => ISN = ReadMemory(ediabas, 0x7940, 0x7945, ReadingText));
                        if (ISN.SequenceEqual(ProtectedRead))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Could not read ISN, please flash RSA Bypass to enable reading";
                            });
                            return;
                        }
                        byte[] CASISN = { ISN[2], ISN[1] };
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "CAS ISN: " + BitConverter.ToString(CASISN) + "\nDME ISN: " + BitConverter.ToString(ISN);                           
                        });

                        
                        DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                        SaveFileDialog saveFile = new SaveFileDialog();
                        {
                            saveFile.FileName = "ISN";
                            saveFile.InitialDirectory = SaveDirectory.FullName;
                            Console.WriteLine(saveFile.InitialDirectory);
                            saveFile.Filter = "Binary|*.bin|Original File|*.ori|All Files|*.*";
                            try
                            {
                                Nullable<bool> result = saveFile.ShowDialog();
                                if (result == true)
                                    File.WriteAllBytes(saveFile.FileName, ISN);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception caught in process: {0}", ex);
                                MessageBox.Show("Error trying to save file");
                            }

                        }
                    }

                    if (Global.HW_Ref == "0569QT0")
                    {
                        ReadingText = "Reading EWS4 SK";
                        await Task.Run(() => EWS4_SK = ReadMemory(ediabas, 0x7948, 0x797F, ReadingText));

                        if (EWS4_SK.Take(0x8).ToArray().SequenceEqual(EWS4_SK_Header))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Secret Key: " + BitConverter.ToString(EWS4_SK.Skip(0x8).Take(0x10).ToArray());
                            });
                        }

                        else
                        {
                            ReadingText = "Reading RAM";

                            await Task.Run(() => InjRAMDump = ReadMemory(ediabas, start, end, ReadingText));


                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Searching for secret key";
                            });

                            int IndexOfSK = FindSK(InjRAMDump);
                            if (IndexOfSK == -1)
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    statusTextBlock.Text = "Could not find secret key";
                                });
                                return;
                            }

                            else
                            {
                                byte[] SK = InjRAMDump.Skip(IndexOfSK).Take(0x30).ToArray();
                                this.Dispatcher.Invoke(() =>
                                {
                                    statusTextBlock.Text = "Secret Key: " + BitConverter.ToString(SK.Take(0x10).ToArray());
                                    Console.WriteLine("Secret Key Found @" + (IndexOfSK.ToString("x")));
                                });
                                EWS4_SK = EWS4_SK_Header.Concat(SK).ToArray();
                            }


                        }

                        DirectoryInfo SaveDirectory = Directory.CreateDirectory(Global.VIN);
                        SaveFileDialog saveFile = new SaveFileDialog();
                        {
                            saveFile.FileName = "EWS4_SK";
                            saveFile.InitialDirectory = SaveDirectory.FullName;
                            saveFile.Filter = "Binary|*.bin|Original File|*.ori|All Files|*.*";
                            try
                            {
                                Nullable<bool> result = saveFile.ShowDialog();
                                if (result == true)
                                    File.WriteAllBytes(saveFile.FileName, EWS4_SK);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception caught in process: {0}", ex);
                                MessageBox.Show("Error trying to save file");
                            }
                        }
                    }
                }
            }
        }
    
        private byte[] ReadMemory(EdiabasNet ediabas, uint start, uint end, string ReadingText)
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_AWAYMODE_REQUIRED);

            byte[] MemoryDump = { };
            byte[] MemoryRead = { };
            byte[] Result = { };
            uint length = end - start + 1;
            uint lengthRemaining = length;
            uint segLength = 0x63;
            uint bytesRead = 0;

            //Console.WriteLine(start.ToString("x"));

            while (bytesRead < length)
            {
                if (lengthRemaining < segLength)
                    segLength = lengthRemaining;
                if (!ExecuteJob(ediabas, "ram_lesen", + start + ";" + segLength))
                {
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                    return MemoryDump;
                }

                if (ReadingText != String.Empty)
                {
                    this.Dispatcher.BeginInvoke((Action)(() =>
                        {
                    statusTextBlock.Text = ReadingText + " @ 0x" + start.ToString("x");
                    }));
                }
                

                bytesRead += segLength;
                MemoryRead = GetResult_ByteArray("RAM_LESEN_WERT", ediabas.ResultSets);

                start = start + segLength;
                lengthRemaining = lengthRemaining - segLength;


                if (length > 255)
                {
                    uint progress = bytesRead * 4096 / length;
                    UpdateProgressBar(progress);
                }

                MemoryDump = MemoryDump.Concat(MemoryRead).ToArray();


            }
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            return MemoryDump;
        }

        private bool IsRSABypassed(EdiabasNet ediabas)
        {
            //uint RSASegments1 = 0x101C0;
            uint RSASegmentsLocation = 0x10204;

            byte[] StockRSASegments = 
            {
                0x00, 0x00, 0x00, 0x05, 0x00, 0x07, 0x00, 0x00, 0x00, 0x07, 0x00, 0x3F, 0x00, 0x07, 0x01, 0xC0,
                0x00, 0x07, 0xFF, 0xFE, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0xFF, 0xFF, 0x00, 0x04, 0x00, 0x00,
                0x00, 0x06, 0xFF, 0xFF, 0x00, 0x45, 0x00, 0x00, 0x00, 0x5F, 0xFF, 0xFF,
            };


            //byte[] rsa_segments_1_injection = ReadMemory(ediabas, RSASegments1, RSASegments1 + 0x2C - 1, String.Empty);
            byte[] rsa_segments_2_injection = ReadMemory(ediabas, RSASegmentsLocation, RSASegmentsLocation + 0x2C - 1, String.Empty);
            //byte[] rsa_segments_1_ignition = ReadMemory(ediabas, RSASegments1 + 0x800000, RSASegments1 + 0x800000 + 0x2C - 1, String.Empty);
            byte[] rsa_segments_2_ignition = ReadMemory(ediabas, RSASegmentsLocation + 0x800000, RSASegmentsLocation + 0x800000 + 0x2C - 1, String.Empty);

            if (StockRSASegments.SequenceEqual(rsa_segments_2_injection) || StockRSASegments.SequenceEqual(rsa_segments_2_ignition))
                return false;

            return true;
        }

        private async Task<bool> FlashDME_Data(byte[] tune, bool fromRSABypassRoutine)
        {
            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            
            bool success = true;
            bool IsSigValid = ChecksumsSignatures.IsParamSignatureValid(tune); ;
            string FlashingText = string.Empty;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            { 
                if (!IsSigValid && !IsRSABypassed(ediabas) && Global.HW_Ref != "0569Q60")
                {
                    string msg = "Warning: We detected you are trying to flash a non-stock tune without our RSA bypass installed. " +
                                 "This is likely to fail unless you have someone else's RSA bypass installed.\n\nWould you like to continue?";
                    MessageBoxResult result =  MessageBox.Show(msg, "No RSA Bypass Detected",  MessageBoxButton.YesNo,  MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Tune write cancelled";
                        });

                        return false;
                    }
                }
            }



            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"],  ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {
                    await Task.Run(() =>
                {
                    if (!ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch")) 
                    {
                        Console.WriteLine("Failed to set address to DME");
                    }
                    ExecuteJob(ediabas, "aif_lesen", string.Empty);//Not sure if all this is really necessary when we know the information already, but WinKFP does it before every flash

                    ExecuteJob(ediabas, "hardware_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "daten_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "flash_programmier_status_lesen", string.Empty);

                    ExecuteJob(ediabas, "FLASH_ZEITEN_LESEN", string.Empty);

                    ExecuteJob(ediabas, "FLASH_BLOCKLAENGE_LESEN", string.Empty);
                    
                    if (!RequestSecurityAccess(ediabas))
                    {
                        success = false;
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Security Access Denied";
                        });
                    }
                });

                uint eraseStart = 0x70000;
                uint eraseBlock = 0x10000;
                uint flashStart = 0x70000;
                uint flashEnd = 0x7FFFF;
                uint IgnitionOffset = 0x800000;

                byte[] toFlash_Inj = new byte[0];
                byte[] toFlash_Ign = new byte[0];


                toFlash_Inj = ChecksumsSignatures.CorrectParameterChecksums(tune.Take(0x10000).ToArray());
                toFlash_Ign = ChecksumsSignatures.CorrectParameterChecksums(tune.Skip(0x10000).Take(0x10000).ToArray());


                if (!ExecuteJob(ediabas, "normaler_datenverkehr", "nein;nein;ja"))
                    return false;
                if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;nein"))
                    return false;



                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Erasing Flash";
                });
                await Task.Run(() => success = EraseECU(ediabas, eraseBlock, eraseStart));
                //await Task.Run(() => success = EraseECU(ediabas, eraseBlock+IgnitionOffset, eraseStart+IgnitionOffset));





                if (success)
                {
                    if (fromRSABypassRoutine)
                        FlashingText = "Injection: Preparing for program flash";
                    if (!fromRSABypassRoutine)
                        FlashingText = "Injection: Flashing Tune";
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_Inj.Take(0x80).ToArray(), flashStart, flashStart + 0x7F, FlashingText)); //0x70080 -> 0x700FF is protected on MSS60.
                }

                if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_Inj.Skip(0x100).ToArray(), flashStart + 0x100, flashEnd, FlashingText));

                if (success)
                {
                    if (Global.HW_Ref == "0569Q60" && !IsSigValid)
                    {
                        byte[] MSS65RSABypass = //Those bytes are *not* protected on the MSS65, and can in fact be used to bypass RSA altogether
                                                //Note: Doing the same trick on the program rather than tune is a guaranteed brick. Don't do it. 
                        {
                        0xAF, 0xFE, 0x08, 0x15, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    };
                        await Task.Run(() => success = FlashBlock(ediabas, MSS65RSABypass, flashStart + 0x80, flashStart + 0xBF, FlashingText));
                    }
                }

                if (success)
                {
                    if (fromRSABypassRoutine)
                        FlashingText = "Ignition: Preparing for program flash";
                    if (!fromRSABypassRoutine)
                        FlashingText = "Ignition: Flashing Tune";
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_Ign.Take(0x80).ToArray(), flashStart + IgnitionOffset, flashStart + 0x7F + IgnitionOffset, FlashingText));
                }
                if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_Ign.Skip(0x100).ToArray(), flashStart + 0x100 + IgnitionOffset, flashEnd + IgnitionOffset, FlashingText));

                if (success)
                {
                    if (Global.HW_Ref == "0569Q60" && !IsSigValid)
                    {
                        byte[] MSS65RSABypass =
                        {
                        0xAF, 0xFE, 0x08, 0x15, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                         };
                        await Task.Run(() => success = FlashBlock(ediabas, MSS65RSABypass, flashStart + 0x80 + IgnitionOffset, flashStart + 0xBF + IgnitionOffset, FlashingText));
                    }
                }

                if (success)
                {
                    await Task.Run(() =>
                    {
                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;ja"))
                        {
                            success = false;
                            NotFlashing(this, null);
                            return;
                        }


                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))
                        {
                            success = false;
                            NotFlashing(this, null);
                            return;
                        }
                        
                        if (Global.HW_Ref != "0569Q60" || IsSigValid) 
                                                        //If we're writing the "RSA Passed" bytes directly, no need to do a signature check that will fail.
                                                        //When the DME does reboot, it will be in normal operating mode
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Checking signature";
                            });
                            this.Dispatcher.Invoke(() =>
                            {
                                ProgressDME.IsIndeterminate = true;
                            });
                            if (!ExecuteJob(ediabas, "FLASH_SIGNATUR_PRUEFEN", "Daten;35"))
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    ProgressDME.IsIndeterminate = false;
                                });
                                this.Dispatcher.Invoke(() =>
                                {
                                    statusTextBlock.Text = "Signature check failed";
                                });
                                Console.WriteLine(GetResult_String("JOB_STATUS", ediabas.ResultSets));
                                success = false;
                                NotFlashing(this, null);
                                if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                                {
                                    return;
                                }
                                return;
                            }
                            this.Dispatcher.Invoke(() =>
                            {
                                ProgressDME.IsIndeterminate = false;
                            });
                        }
                        if (!fromRSABypassRoutine)
                            NotFlashing(this, null);
                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))
                        {
                            success = false;
                            return;
                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Resetting ECU";
                        });
                        if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Failed to reset ECU";
                            });

                            return;
                        }
                    });

                    if (success)
                    {

                        this.Dispatcher.Invoke(() =>
                        {

                            if (fromRSABypassRoutine)
                                statusTextBlock.Text = "Now flashing remaining program code";
                            else
                                statusTextBlock.Text = "Tune flash success";


                        });
                        UpdateProgressBar(0);
                    }
                    /*else
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Flash failed";
                        });
                    }*/
                }
                IdentDME();
                //NotFlashing(this, null);
                return success;
            }
            
        }

        private async Task<bool> Flashfull(byte[] full, bool bypassRSA, bool bypassRSAFast)
        {

            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            bool success = true;
            String FlashingText = String.Empty;

            bool SigValid = ChecksumsSignatures.IsProgramSignatureValid(full);
            bool RSABypassInstalled = false;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                RSABypassInstalled = IsRSABypassed(ediabas);
                if (!SigValid && !RSABypassInstalled)
                {
                    string msg = "Warning: We detected you are trying to flash a non-stock program without having our RSA bypass installed. " +
                                 "This is likely to fail unless you know your Program RSA check is bypassed some other way.\n\nWould you like to continue?";
                    MessageBoxResult result =  MessageBox.Show(msg, "No RSA Bypass Detected", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Program flash cancelled";
                        });

                        return false;
                    }
                }
            }

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"],  ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {

                uint flashStart_Section1 = 0x10000;
                uint flashEnd_Section1 = 0x1FFFF;


                uint ignitionOffset = 0x800000;


                byte[] injection = full.Take(0x280000).ToArray();
                byte[] ignition = full.Skip(0x280000).Take(0x280000).ToArray();

                if (!(SigValid && !RSABypassInstalled) || bypassRSA)
                {
                    injection = ChecksumsSignatures.BypassRSA(injection);
                    ignition = ChecksumsSignatures.BypassRSA(ignition);
                }

                if (!bypassRSA)
                {
                    injection = ChecksumsSignatures.CorrectProgramChecksums(injection);
                    ignition = ChecksumsSignatures.CorrectProgramChecksums(ignition);
                }

                byte[] toFlash_InjInt_Sect1 = injection.Skip(0x10000).Take(0x10000).ToArray();
                byte[] toFlash_InjInt_Sect2 = injection.Skip(0x20000).Take(0x50000).ToArray();
                byte[] toFlash_InjExt = injection.Skip(0xD0000).Take(0xB0000).ToArray();

                byte[] toFlash_IgnInt_Sect1 = ignition.Skip(0x10000).Take(0x10000).ToArray();
                byte[] toFlash_IgnInt_Sect2 = ignition.Skip(0x20000).Take(0x50000).ToArray();
                byte[] toFlash_IgnExt = ignition.Skip(0xD0000).Take(0xB0000).ToArray();

                await Task.Run(() =>
                {

                    if (!ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch"))
                    {
                        Console.WriteLine("Failed to set address to DME");
                    }


                    ExecuteJob(ediabas, "aif_lesen", string.Empty);

                    ExecuteJob(ediabas, "hardware_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "daten_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "flash_programmier_status_lesen", string.Empty);

                    ExecuteJob(ediabas, "FLASH_ZEITEN_LESEN", string.Empty);



                    if (!RequestSecurityAccess(ediabas))
                    {
                        success = false;
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Security Access Denied";
                        });
                    }
                });

                uint eraseStart = 0x10000;
                uint eraseBlock = 0x13f6c8;

                uint eraseStartRSA = 0x70000;
                uint eraseBlockRSA = 0x10000;

                uint flashStart_Section2 = 0x20000;
                uint flashEnd_Section2 = 0x6FFFF;

                uint flashExtStart = 0x450000;
                uint flashExtEnd_Inj = BitConverter.ToUInt32(injection.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7;
                uint flashExtEnd_Ign = BitConverter.ToUInt32(ignition.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7;

                if (flashExtEnd_Inj > 0x4FFFFF)
                    flashExtEnd_Inj = 0x4FFFFF;
                if (flashExtEnd_Ign > 0x4FFFFF)
                    flashExtEnd_Ign = 0x4FFFFF;

                Console.WriteLine(flashExtEnd_Inj.ToString("x"));
                Console.WriteLine(flashExtEnd_Ign.ToString("x"));

                await Task.Run(() =>
                    {

                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "nein;nein;ja"))
                        {
                            Console.WriteLine("Failed to shut down vehicle electronics");
                            //success = false;
                        }

                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;nein"))
                        {
                            Console.WriteLine("Failed to shut down vehicle electronics");
                            //success = false;
                        }
                    });


                if (success)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Erasing ECU";
                    });

                    if (bypassRSA && bypassRSAFast)
                    {
                        await Task.Run(() => success = EraseECU(ediabas, eraseBlockRSA, eraseStartRSA));
                        //await Task.Run(() => success = EraseECU(ediabas, eraseBlockRSA, eraseStartRSA + ignitionOffset));
                    }


                    else
                    {
                        await Task.Run(() => success = EraseECU(ediabas, eraseBlock, eraseStart));
                        //await Task.Run(() => success = EraseECU(ediabas, eraseBlock, eraseStart + ignitionOffset)); 
                    }
                }

                if (success)
                {
                    if (bypassRSA && bypassRSAFast)
                        FlashingText = "Injection: Flashing RSA Bypass";
                    else
                        FlashingText = "Injection: Flashing Boot Region";
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect1.Take(0x80).ToArray(), flashStart_Section1, flashStart_Section1 + 0x7F, FlashingText));
                }//On MSS60, 0x10080 -> 0x10100 is protected

                if (success)
                     await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect1.Skip(0x100).ToArray(), flashStart_Section1 + 0x100 , flashEnd_Section1, FlashingText));

                if (success)
                {
                    if (!bypassRSA || !bypassRSAFast)
                    {
                        FlashingText = "Injection: Flashing Program Region 1";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect2, flashStart_Section2, flashEnd_Section2, FlashingText));
                        FlashingText = "Injection: Flashing Program Region 2";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjExt, flashExtStart, flashExtEnd_Inj, FlashingText));

                    }
                }

                if (success)
                {
                    if (bypassRSA && bypassRSAFast)
                        FlashingText = "Ignition: Flashing RSA Bypass";
                    else
                        FlashingText = "Ignition: Flashing Boot Region";
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect1.Take(0x80).ToArray(), flashStart_Section1 + ignitionOffset, flashStart_Section1 + 0x7F + ignitionOffset, FlashingText));
                }
                if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect1.Skip(0x100).ToArray(), flashStart_Section1 + 0x100 + ignitionOffset, flashEnd_Section1 + ignitionOffset, FlashingText));


                if (!bypassRSA || !bypassRSAFast)
                {
                    if (success)
                    {
                        FlashingText = "Ignition: Flashing Program Region 1";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect2, flashStart_Section2 + ignitionOffset, flashEnd_Section2 + ignitionOffset, FlashingText));
                    }

                    if (success)
                    {
                        FlashingText = "Ignition: Flashing Program Region 2";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnExt, flashExtStart + ignitionOffset, flashExtEnd_Ign + ignitionOffset, FlashingText));
                    }              
                }


                if (success)
                {
                    await Task.Run(() =>
                    {

                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;ja"))
                        {
                            success = false;
                            NotFlashing(this, null);
                            return;
                        }

                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))

                        {
                            success = false;
                            NotFlashing(this, null);
                            return;
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Checking signature";
                        });
                        this.Dispatcher.Invoke(() =>
                        {
                            ProgressDME.IsIndeterminate = true;
                        });
                        if (!ExecuteJob(ediabas, "FLASH_SIGNATUR_PRUEFEN", "Programm;64"))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                ProgressDME.IsIndeterminate = false;
                            });
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "Signature check failed";
                            });
                            success = false;
                            NotFlashing(this, null);
                            if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                            {
                                return;
                            }
                            return;
                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            ProgressDME.IsIndeterminate = false;
                        });
                        if (!bypassRSA)
                            NotFlashing(this, null);
                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))
                        {
                            success = false;
                            return;
                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Resetting ECU";
                        });
                        if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                        {
                            return;
                        }
                    });

                    if (success)
                    {

                        this.Dispatcher.Invoke(() =>
                        {
                            if(bypassRSA)
                                statusTextBlock.Text = "RSA Bypass accepted. Preparing DME for program";
                            else
                                statusTextBlock.Text = "Program flash success. Flash tune to finish";

                            UpdateProgressBar(0);
                        });


                        if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "Flash failed";
                        });
                    }
                }
                IdentDME();
                return success;
            }
        }

        private bool FlashBlock(EdiabasNet ediabas, byte[] toFlash, uint blockStart, uint blockEnd, string FlashingText)
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_AWAYMODE_REQUIRED); //Should prevent system from going idle while flashing

            uint blockStartOrig = blockStart;
            uint blockLength = blockEnd - blockStart + 1;

            byte[] flashAddressSet = new Byte[22];
            flashAddressSet[0] = 1;
            flashAddressSet[21] = 3;

            BitConverter.GetBytes(blockStart).CopyTo(flashAddressSet, 17);
            BitConverter.GetBytes(blockLength).CopyTo(flashAddressSet, 13);
            //See ediabas comments on flash_schreiben_adresse to see details on how this array should be set


            byte[] flashHeader = new Byte[21];
            byte[] three = { 3 };
            int flashSegLength = 0xFE;
            flashHeader[0] = 1;
            flashHeader[13] = (byte)flashSegLength;

            string flashAddressJob = "flash_schreiben_adresse";
            string flashJob = "flash_schreiben";
            string flashEndJob = "flash_schreiben_ende";
            
            if (!ExecuteJob(ediabas, flashAddressJob, flashAddressSet))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Failed to set flash address @ 0x" +blockStart.ToString("x");
                    Console.WriteLine("Flash Address Message: " + BitConverter.ToString(GetResult_ByteArray("_TEL_AUFTRAG", ediabas.ResultSets)));
                });
                NotFlashing(this, null);
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                return false;
            }

            Flashing(this, null);
            while (blockLength > 0)
            {
                if (blockLength < flashSegLength)
                {
                    flashSegLength = (int)blockLength;
                    flashHeader[13] = (byte)flashSegLength;
                }
                BitConverter.GetBytes(blockStart).CopyTo(flashHeader, 17);

                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    statusTextBlock.Text = FlashingText + " @ 0x" + blockStart.ToString("x");
                }));

                if (!ExecuteJob(ediabas, flashJob, flashHeader.Concat(toFlash.Skip((int)(blockStart) - (int)blockStartOrig).Take(flashSegLength)).Concat(three).ToArray())) //See Ediabas comments for details on what the flash message should look like
                {
                    NotFlashing(this, null);
                    this.Dispatcher.Invoke(() =>
                    {
                        Console.WriteLine("Flash failed at 0x" + blockStart.ToString("X") + ". Resetting DME.");
                        statusTextBlock.Text = "Flash failed at 0x" + blockStart.ToString("X") + ". Resetting DME.";
                    });
                    if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            Console.WriteLine("Error Resetting ECU");
                        });
                        SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                        return false;
                    }
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                    return false;
                }
                blockStart += (uint)flashSegLength;
                blockLength -= (uint)flashSegLength;

                if (blockEnd - blockStartOrig > 255)
                {
                    uint progress = (blockStart - blockStartOrig) * 4096 / (blockEnd - blockStartOrig);
                    UpdateProgressBar(progress);
                }

            }

            if (!ExecuteJob(ediabas, flashEndJob, flashAddressSet))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Failed to end flash job";
                });
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                NotFlashing(this, null);
                return false;
            }
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS); //Allow system to idle
            return true;
        }

        private bool EraseECU(EdiabasNet ediabas, uint blockLength, uint blockStart)
        {
            string flashEraseJob = "flash_loeschen";

            //byte[] eraseCommand = { 01, 01, 00, 00, 0xFE, 00, 00, 00, 00, 0xFF, 00, 00, 00, 0x44, 0xEA, 01, 00, 00, 00, 0x84, 00, 03 };
            byte[] eraseCommand = new Byte[22];
            eraseCommand[0] = 1;
            eraseCommand[1] = 1;
            eraseCommand[4] = 0xFA;
            eraseCommand[9] = 0xFF;

            BitConverter.GetBytes(blockStart).CopyTo(eraseCommand, 17); //Start address
            BitConverter.GetBytes(blockLength).CopyTo(eraseCommand, 13); //Length - doesn't really matter for erases. 
                                                                         //Erasing anything in the program space will erase the entire program space, erasing anything in the parameter space will erase entire parameter space)
            Flashing(this, null);
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = true;
            });
            if (!ExecuteJob(ediabas, flashEraseJob, eraseCommand))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Erase failed";
                });
                NotFlashing(this, null);
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            //NotFlashing(this, null);
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = false;
            });
            return true;
        }

        //Security Access
        private bool RequestSecurityAccess(EdiabasNet ediabas)
        { 

            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();

            this.Dispatcher.Invoke(() =>
            {

                ProgressDME.IsIndeterminate = true;
                statusTextBlock.Text = "Requesting Security Access";
            });

            if (!ExecuteJob(ediabas, "seriennummer_lesen", string.Empty))
            {
                Console.WriteLine("Failed to get serial number");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }

            byte[] serialReply = GetResult_ByteArray("_TEL_ANTWORT", ediabas.ResultSets);
            //Console.WriteLine(BitConverter.ToString(serialReply));
            byte[] serialNumber = serialReply.Skip(serialReply.Length - 5).Take(4).ToArray(); //DME uses last 4 bytes of serial number in authentication message
            byte[] userID = new byte[4]; //user ID can be any 4 bytes. 
            Random rng = new Random();
            rng.NextBytes(userID); //Probably a bit wasteful to use a random number generator over pulling 4 bytes out of my ass, but whatever.

            if (!ExecuteJob(ediabas, "authentisierung_zufallszahl_lesen", "3;0x" + BitConverter.ToUInt32(userID.Reverse().ToArray(), 0).ToString("X")))//Request random number, passing the "userID" generated above as an argument
            {
                Console.WriteLine("Failed to get random number");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            byte[] seed = GetResult_ByteArray("ZUFALLSZAHL", ediabas.ResultSets); //DME sends a random number

            Console.WriteLine(BitConverter.ToString(userID.Concat(serialNumber.Concat(seed)).ToArray()));           
            if (!ExecuteJob(ediabas, "authentisierung_start", ChecksumsSignatures.GetSecurityAccessMessage(userID, serialNumber, seed))) //Sign message using level 3 private key. If DME decrypts successfully and it matches its own calculation, security access is granted
            {
                Console.WriteLine("Failed to authenticate tester");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }


            if (!ExecuteJob(ediabas, "diagnose_mode", "ECUPM"))
            {
                Console.WriteLine("Could Not Request ECUPM");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = false;
            });
            return true;//Should be in ECU Programming Mode now

        }


        //The below is basically ripped straight out of example ediabaslib code
        private EdiabasNet StartEdiabas(string port, string path, string sgbd)
        {
            EdiabasNet ediabas = new EdiabasNet();
            EdInterfaceBase edInterface;
            edInterface = new EdInterfaceObd();


            ediabas.EdInterfaceClass = edInterface;
            ediabas.ProgressJobFunc = ProgressJobFunc;
            ediabas.ErrorRaisedFunc = ErrorRaisedFunc;

            //((EdInterfaceObd)edInterface).ComPort = "FTDI0";
            ((EdInterfaceObd)edInterface).ComPort = port;

            ediabas.ArgBinary = null;
            ediabas.ArgBinaryStd = null;
            ediabas.ResultsRequests = string.Empty;

            ediabas.SetConfigProperty("EcuPath", path);
            ediabas.ResultsRequests = string.Empty;

            try
            {
                ediabas.ResolveSgbdFile(sgbd);
            }

            catch (Exception ex2)
            {
                System.Diagnostics.Debug.WriteLine("ResolveSgbdFile failed: " + EdiabasNet.GetExceptionText(ex2));
            }

            return ediabas;
        }

        private static void ProgressJobFunc(EdiabasNet ediabas)
        {
            string infoProgressText = ediabas.InfoProgressText;
            int infoProgressPercent = ediabas.InfoProgressPercent;
            string text = string.Empty;
            if (infoProgressPercent >= 0)
            {
                text += string.Format("{0,3}% ", infoProgressPercent);
            }
            if (infoProgressText.Length > 0)
            {
                text += string.Format("'{0}'", infoProgressText);
            }
            if (text.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine("Progress: " + text);
            }
        }

        private static void ErrorRaisedFunc(EdiabasNet.ErrorCodes error)
        {
            string errorDescription = EdiabasNet.GetErrorDescription(error);
            System.Diagnostics.Debug.WriteLine("Error occured: 0x{0:X08} {1}", new object[]
            {
        (uint)error,
        errorDescription
            });
        }

        private static string GetResult_String(string resultName, List<Dictionary<string, EdiabasNet.ResultData>> resultSets)
        {
            string result = string.Empty;
            if (resultSets != null)
            {
                foreach (Dictionary<string, EdiabasNet.ResultData> dictionary in resultSets)
                {
                    foreach (string key in from x in dictionary.Keys
                                           orderby x
                                           select x)
                    {
                        EdiabasNet.ResultData resultData = dictionary[key];
                        if (resultData.Name == resultName && resultData.OpData is string)
                            result = (string)resultData.OpData;
                    }
                }
            }
            return result;
        }

        private static byte[] GetResult_ByteArray(string resultName, List<Dictionary<string, EdiabasNet.ResultData>> resultSets)
        {
            byte[] result = null;
            if (resultSets != null)
            {
                foreach (Dictionary<string, EdiabasNet.ResultData> dictionary in resultSets)
                {
                    foreach (string key in from x in dictionary.Keys
                                           orderby x
                                           select x)
                    {
                        EdiabasNet.ResultData resultData = dictionary[key];
                        if (resultData.Name == resultName && resultData.OpData.GetType() == typeof(byte[]))
                            result = (byte[])resultData.OpData;

                    }
                }
            }
            return result;
        }

        private static bool ExecuteJob(EdiabasNet ediabas, string Job, string Arg)
        {
            ediabas.ArgString = Arg;
            try
            {
                ediabas.ExecuteJob(Job);
            }
            catch (Exception ex)
            {
                if (ediabas.ErrorCodeLast == EdiabasNet.ErrorCodes.EDIABAS_ERR_NONE)
                {
                    System.Diagnostics.Debug.WriteLine("Job execution failed: " + EdiabasNet.GetExceptionText(ex));
                    System.Diagnostics.Debug.WriteLine("");

                }
                return false;
            }
            return (GetResult_String("JOB_STATUS", ediabas.ResultSets) == "OKAY");
        }

        private static bool ExecuteJob(EdiabasNet ediabas, string Job, byte[] Arg)
        {
            ediabas.ArgBinary = Arg;
            try
            {
                ediabas.ExecuteJob(Job);
            }
            catch (Exception ex)
            {
                if (ediabas.ErrorCodeLast == EdiabasNet.ErrorCodes.EDIABAS_ERR_NONE)
                {
                    System.Diagnostics.Debug.WriteLine("Job execution failed: " + EdiabasNet.GetExceptionText(ex));
                    System.Diagnostics.Debug.WriteLine("");
                }
                return false;
            }
            return (GetResult_String("JOB_STATUS", ediabas.ResultSets) == "OKAY");
        }

        private void IdentifyDME_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar(0);
            IdentDME();
        }

        private void ReadTune_Click(object sender, RoutedEventArgs e)
        {
            ReadDME();
        }

        private void ReadFull_Click(object sender, RoutedEventArgs e)
        {
            ReadDME_Full(true);
        }

        private void Read_Full_Long_Click(object sender, RoutedEventArgs e)
        {
            ReadDME_Full(false);
        }

        private void Read_ISN_SK_Click(object sender, RoutedEventArgs e)
        {
            ReadISN_SK();
        }

        private void Read_RAM_Click(object sender, RoutedEventArgs e)
        {
            ReadRAM();

        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar(0);
            LoadFile_1();
        }

        private void FlashData_Click(object sender, RoutedEventArgs e)
        {
            byte[] tune = new byte[0];
            if (IsFullBinLoaded() || Global.openedFlash.Length > 0x20000)
                tune = Global.openedFlash.Skip(0x70000).Take(0x10000).Concat(Global.openedFlash.Skip(0x2F0000).Take(0x10000)).ToArray();
            else
                tune = Global.openedFlash;
            FlashDME_Data(tune, false);
        }

        private void FlashProgram_Click(object sender, RoutedEventArgs e)
        {
            Flashfull(Global.openedFlash, false, false);
        }

        private void FlashRSA_Bypass_Fast_Click(object sender, RoutedEventArgs e)
        {
            rsabypasstasks(true);
        }

        private void FlashRSA_Bypass_Slow_Click(object sender, RoutedEventArgs e)
        {
            rsabypasstasks(false);
        }

        private void AppExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Url_Click(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

    }
}


