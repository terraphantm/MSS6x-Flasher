using EdiabasLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

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

        private bool DMEBusy = false;

        private bool FullbinLoaded = false;

        private static String WildCardToRegular(String value)
        {
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }

        private void ClearText_DisableButtons()
        {
            HWRef_Box.Content = String.Empty;
            ZIF_Box.Content = String.Empty;
            Calibration_Reference_Box.Content = String.Empty;
            programStatus_Box.Content = String.Empty;
            VIN_Box.Content = String.Empty;
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                statusTextBlock.Text = String.Empty;
            }));
            AdvancedMenu.IsEnabled = false;
            ReadFull_button.IsEnabled = false;
            ReadTune_button.IsEnabled = false;
            LoadFile_button.IsEnabled = false;
        }

        private void DisableButtons(object sender, EventArgs e)
        {
            DMEBusy = true;
            this.Dispatcher.Invoke(() =>
            {
            FunctionStack.IsEnabled = false;
            AdvancedMenu.IsEnabled = false;
            });
        }

        private void ReenableButtons(object sender, EventArgs e)
        {
            DMEBusy = false;
            this.Dispatcher.Invoke(() =>
            {
                FunctionStack.IsEnabled = true;
                AdvancedMenu.IsEnabled = true;
            });
        }

        private void ResetDME()
        {
            bool success = true;
            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                success = ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty);
            }
            if (!success)
            {
                using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
                {
                    success = ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty);
                }
            }
            IdentDME();
            if (!success)
            {
            }
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
            if (this.DMEBusy)
            {
                string msg = "DME is busy, exiting now may have unpredictable results.\n\nAre you sure you want to exit?";
                MessageBoxResult result =
                  MessageBox.Show(
                    msg,
                    "DME Busy",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    // If user doesn't want to close, cancel closure
                    e.Cancel = true;
                }
                if (result == MessageBoxResult.Yes)
                {
                    ResetDME();
                    Environment.Exit(-2);
                }
            }
            else
            {
                Environment.Exit(0);
            }
        }


        private void  IdentDME()
        {
            string DMEType;
            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {
                ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch");
                uint SW_version_revision_uint = 0;
                ExecuteJob(ediabas, "aif_lesen", string.Empty);

                Global.VIN = GetResult_String("AIF_FG_NR", ediabas.ResultSets);

                ExecuteJob(ediabas, "hardware_referenz_lesen", string.Empty);

                Global.HW_Ref = GetResult_String("HARDWARE_REFERENZ", ediabas.ResultSets);

                ExecuteJob(ediabas, "ident", string.Empty);
                try
                {
                    byte[] SW_vers_interal_bytes = GetResult_ByteArray("_TEL_ANTWORT", ediabas.ResultSets).Skip(0x1f).Take(3).ToArray();
                    Global.Prog_Vers_internal_uint = uint.Parse(SW_vers_interal_bytes[0].ToString() + SW_vers_interal_bytes[1].ToString("00"));
                    SW_version_revision_uint = (uint)SW_vers_interal_bytes[2];
                }
                catch
                {

                }


                ExecuteJob(ediabas, "daten_referenz_lesen", string.Empty);

                string Data_Ref = GetResult_String("DATEN_REFERENZ", ediabas.ResultSets);
                if (Data_Ref.Length > 12)
                    Data_Ref = Data_Ref.Substring(12);

               

                ExecuteJob(ediabas, "zif_lesen", string.Empty);
                string zif = string.Empty;
                if (GetResult_String("ZIF_PROGRAMM_REFERENZ", ediabas.ResultSets).Contains(Global.HW_Ref))
                    try
                    {
                        zif = GetResult_String("ZIF_PROGRAMM_REFERENZ", ediabas.ResultSets).Substring(7);
                    }
                    catch
                    {
                    }
                else
                {
                    ExecuteJob(ediabas, "zif_backup_lesen", string.Empty);
                    if (GetResult_String("ZIF_BACKUP_PROGRAMM_REFERENZ", ediabas.ResultSets).Contains(Global.HW_Ref))
                        try
                        {
                            zif = GetResult_String("ZIF_BACKUP_PROGRAMM_REFERENZ", ediabas.ResultSets).Substring(7);
                        }
                        catch
                        {
                        }
                }

                Global.ZIF = zif;
                string programming_status = String.Empty;
                ExecuteJob(ediabas, "flash_programmier_status_lesen", string.Empty);
                byte[] programmingStatusReply = GetResult_ByteArray("_TEL_ANTWORT", ediabas.ResultSets);
                try
                {
                    Global.programmingStatusByte = programmingStatusReply[5];
                }
                catch { }

                if (Global.programmingStatusByte == 7 || Global.programmingStatusByte == 8)
                {
                    ResetDME();
                }
                try
                {
                    programming_status = Global.programmingStatusStringArray[Global.programmingStatusByte];
                }
                catch { }

                if (programming_status == String.Empty)
                    programming_status = "Unknown Programming Status";

                DMEType = "Unknown / Unsuppported";

                if (Global.HW_Ref == "0569Q60")
                    DMEType = "MSS65";
                else if (Global.HW_Ref == "0569QT0")
                    DMEType = "MSS60";        
                else
                    ClearText_DisableButtons();


                this.Dispatcher.Invoke(() =>
                {
                DMEType_Box.Content = DMEType;
                //ReadFull.IsEnabled = true;
                //AdvancedMenu.IsEnabled = true;


                if (DMEType != String.Empty && DMEType != "Unknown / Unsuppported")
                {
                    HWRef_Box.Content = Global.HW_Ref;
                    if (Global.Prog_Vers_internal_uint < 1000 && Global.Prog_Vers_internal_uint >= 100)
                    {
                        if (SW_version_revision_uint != 0 && SW_version_revision_uint < 100)
                        {
                            ZIF_Box.Content = Global.ZIF + " (v" + Global.Prog_Vers_internal_uint + "." + SW_version_revision_uint.ToString("00") + ")";
                        }
                        else
                        {
                            ZIF_Box.Content = Global.ZIF + " (v" + Global.Prog_Vers_internal_uint + ")";
                        }
                    }
                        
                    else
                    {
                        ZIF_Box.Content = Global.ZIF;
                    }
                Calibration_Reference_Box.Content = Data_Ref;
                programStatus_Box.Content = programming_status;
                VIN_Box.Content = Global.VIN;

                AdvancedMenu.IsEnabled = true;
                ReadFull_button.IsEnabled = true;
                ReadTune_button.IsEnabled = true;
                LoadFile_button.IsEnabled = true;
                }
            });
        }
    }


        private void LoadFile_Dialog()
        {
            FlashTune_button.IsEnabled = false;
            FlashProgram_button.IsEnabled = false; 
            
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.InitialDirectory = System.IO.Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory);
            openFile.Filter = "Binary|*.bin|Original File|*.ori|All Files|*.*";
            Nullable<bool> result = openFile.ShowDialog();
            Global.openedFlash = null;
            byte[] file = new byte[0];
            if (result == true)
            {
                file = File.ReadAllBytes(openFile.FileName);
                if(LoadFile_Checks(file))
                {
                    Global.openedFlash = file;
                }
                else
                {
                    FlashTune_button.IsEnabled = false;
                    FlashProgram_button.IsEnabled = false;
                    RSA_Bypass.IsEnabled = false;
                    RestoreStock.IsEnabled = false;
                    Global.openedFlash = null;
                    file = null;
                }
            }

            else
            {
                FlashTune_button.IsEnabled = false;
                FlashProgram_button.IsEnabled = false;
                RSA_Bypass.IsEnabled = false;
                RestoreStock.IsEnabled = false;
                Global.openedFlash = null;
                file = null;
            }

            if (Global.openedFlash != null)
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Loaded " + System.IO.Path.GetFileName(openFile.FileName);
                });
            }
        }

        private bool LoadFile_Checks(byte[] file)
        {
            bool fileValid = false;
            int programmingStatusByte = Global.programmingStatusByte;

            if (file.Length == 0x20000)
            {

                bool allowTuneFlash = false;
                allowTuneFlash =
                    (programmingStatusByte == 0 || programmingStatusByte == 1 ||
                    programmingStatusByte == 6 || programmingStatusByte == 13 || 
                    programmingStatusByte == 14 || programmingStatusByte == 15);

                if (VerifyCalibrationMatch(file, Global.ZIF) && (allowTuneFlash))
                {
                    fileValid = true;
                    //Global.openedFlash = file;
                    FullBinNotLoaded(this, null);
                    FlashTune_button.IsEnabled = true;
                    FlashProgram_button.IsEnabled = false;
                    RSA_Bypass.IsEnabled = false;
                    RestoreStock.IsEnabled = false;
                }
                else
                {
                    FlashTune_button.IsEnabled = false;
                    FlashProgram_button.IsEnabled = false;
                    RSA_Bypass.IsEnabled = false;
                    RestoreStock.IsEnabled = false;
                }
                if (!allowTuneFlash)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "DME program incomplete. Flash a full program first";
                    });
                }
            }
            else if (file.Length == 0x500000)
            {
                if (VerifyProgramMatch(file))
                {
                    fileValid = true;
                    Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
                    FullBinIsLoaded(this, null);
                    FlashProgram_button.IsEnabled = true;
                    RSA_Bypass.IsEnabled = true;
                    string binref1 = System.Text.Encoding.ASCII.GetString(file.Skip(0x10248).Take(0x24).ToArray());
                    string zif = binref1.Substring(7, 5);
                    if (VerifyCalibrationMatch(file.Skip(0x70000).Take(0x10000).Concat(file.Skip(0x70000 + 0x280000).Take(0x10000)).ToArray(), zif) && 
                        VerifyCalibrationMatch(file.Skip(0x70000).Take(0x10000).Concat(file.Skip(0x70000 + 0x280000).Take(0x10000)).ToArray(), Global.ZIF))
                    {
                        FlashTune_button.IsEnabled = true;
                    }
                    if (ChecksumsSignatures.IsProgramSignatureValid(file))
                    {
                        RestoreStock.IsEnabled = true;
                    }
                }
                else
                {
                    FlashTune_button.IsEnabled = false;
                    FlashProgram_button.IsEnabled = false;
                    RSA_Bypass.IsEnabled = false;
                    RestoreStock.IsEnabled = false;
                }
            }
            else
            {
                FlashTune_button.IsEnabled = false;
                FlashProgram_button.IsEnabled = false;
                RSA_Bypass.IsEnabled = false;
                RestoreStock.IsEnabled = false;
            }
            return fileValid;
        }

        private bool VerifyCalibrationMatch(byte[] flash, string zif)
        {
            bool match = false;
            byte[] binheader1 = flash.Take(0x8).ToArray();
            byte[] binheader2 = flash.Skip(0x10000).Take(0x8).ToArray();
            byte[] binheadercompare = { 0x5A, 0x5A, 0x5A, 0x5A, 0xCC, 0xCC, 0xCC, 0xCC };
            //Console.WriteLine(BitConverter.ToString(binheader1));
            //Console.WriteLine(BitConverter.ToString(binheader2));

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

            zif = "*" + zif.Substring(0,3) + "??" /*+ zif.Substring(4)*/
                +"*";
            //Console.WriteLine(zif);

            //Z241E
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



            if (flashExtEnd_Inj > 0x5FFFFF)
                flashExtEnd_Inj = 0x5FFFFF;
            if (flashExtEnd_Ign > 0x5FFFFF)
                flashExtEnd_Ign = 0x5FFFFF;

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
            match = true;
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

        private async Task RSABypassTasks()
        {
            byte[] full = Global.openedFlash;
            byte[] tune = Global.openedFlash.Skip(0x70000).Take(0x10000).Concat(Global.openedFlash.Skip(0x2F0000).Take(0x10000)).ToArray();
            string zif = Global.ZIF;
            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            bool IsSigValid = ChecksumsSignatures.IsProgramSignatureValid(full);

            MessageBoxResult result;
            String msg = string.Empty;

            bool isProgramComplete = false;

            int programmingStatusByte = Global.programmingStatusByte;

            if (programmingStatusByte == 7 || programmingStatusByte == 8)
            {
                ResetDME();
                programmingStatusByte = Global.programmingStatusByte;
            }

            isProgramComplete =
                (programmingStatusByte == 0 || programmingStatusByte == 1 ||
                programmingStatusByte == 6 || programmingStatusByte == 8 ||
                programmingStatusByte == 13 || programmingStatusByte == 14 ||
                programmingStatusByte == 15);

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {

                /*if (!IsSigValid)
                {
                    msg = "The file you loaded is not a stock binary. The RSA bypass requires a stock binary be used. Please reload the appropriate file and try again.";
                    result = MessageBox.Show(msg, "Non-stock binary",  MessageBoxButton.OK,  MessageBoxImage.Error);
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "RSA Bypass Cancelled";
                    });

                    return;

                }*/

                //The current strategy does allow modified programs to be flashed at same time as the RSA bypass itself as long as the version matches what's being installed on the DME
                //Note this is a true requirement and not a cosmetic one -- simply renaming a different program will often result in a brick. 
                //By that same token, if any of the first stage program modifications refer to new or moved subroutines in the 2nd and 3rd stages, then that could also cause a brick which is difficult to anticipate
                //Safest option remains to start with a completely stock binary to do the RSA bypass and then do any program modifications after

                string progRef_FromBinary = System.Text.Encoding.ASCII.GetString(full.Skip(0x10248).Take(0x24).ToArray());
                string progRef_FromDME = System.Text.Encoding.ASCII.GetString(ReadMemory(ediabas, 0x10248, 0x10248 + 0x24 - 1, String.Empty));

                bool doesFileMatchDME = progRef_FromBinary == progRef_FromDME;

                if (!isProgramComplete)
                {
                msg = "Your DME program is incomplete. The RSA bypass routine requires you have a complete program installed beforehand.";
                result = MessageBox.Show(msg, "Program Incomplete", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "RSA Bypass Cancelled";
                });

                    return;
                }

                if (!doesFileMatchDME)
                {
                    msg = "The file you loaded is not the same as what is installed on the DME. The RSA bypass routine requires you use the same program as currently on the DME.";
                    result = MessageBox.Show(msg,  "Program Mismatch", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "RSA Bypass Cancelled";
                    });

                    return;
                }

                if (IsRSASegmentReadable_Bypassed(ediabas)[1])
                {
                    msg = "RSA bypass already present, repeating the step should not be necessary. Do you wish to continue anyway?";


                    result = MessageBox.Show(msg, "RSA Bypass Present", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                        return;

                }

                //The only time this should break is if someone signs a modified file and tries flashing that. If we do ever get the private key then we'd be removing this routine anyway
                if (!IsSigValid)
                {
                    byte[] injection_signature = ReadMemory(ediabas, 0x10100, 0x10183, String.Empty);
                    byte[] ignition_signature = ReadMemory(ediabas, 0x810100, 0x810183, String.Empty);

                    for (int i = 0; i < 0x84; ++i)
                    {
                        full[0x10100 + i] = injection_signature[i];
                        full[0x290100 + i] = ignition_signature[i];
                    }
                }

            }

            msg = "Please ensure you are using a reliable cable (EdiabasLib firmware strongly recommended) and a stable power supply before continuing.\n\nContinue?";
            result = MessageBox.Show(msg, "RSA Bypass", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
                return;

            DisableButtons(this, null);
            bool success = false;
            
            await Task.Run(() => success = FlashDME_Program(full,bypassRSA_stage1:true).Result);
            if (success)
            {
                await Task.Delay(500);
                await Task.Run(() => success = FlashDME_Program(full,bypassRSA_stage2:true).Result);
            }
            if (success)
            {
                LoadFile_Checks(full);
            }
            if (!success)
                statusTextBlock.Text = "RSA Bypass Failed, aborting...";


            ReenableButtons(this, null);
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
                DisableButtons(this, null);
                ReadingText = "Reading Injection RAM";
                await Task.Run(() => Injection = ReadMemory(ediabas, start, end, ReadingText));

                start += 0x800000;
                end += 0x800000;

                ReadingText = "Reading Ignition RAM";
                await Task.Run(() => Ignition = ReadMemory(ediabas, start, end, ReadingText));
                
              

                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = String.Empty;
                });
                ReenableButtons(this, null);
                // SaveFileDialog saveFile = new SaveFileDialog();
                if (Injection.Length == 0x8000 && Ignition.Length == 0x8000)
                {
                    String VIN = Global.VIN;

                    String vers = Global.ZIF;
                    if (vers[0] == 'Z')
                        vers = vers.Substring(1) + "(v" + Global.Prog_Vers_internal_uint.ToString() + ")";

                    string datetime = DateTime.Now.ToString("yyyyMMddHHmm");

                    if (VIN == "")
                    {
                        VIN = "AA00000";
                    }
                    DirectoryInfo SaveDirectory = Directory.CreateDirectory(VIN);
                    File.WriteAllBytes(SaveDirectory + @"\" + VIN + "_" + vers + "_" + "Injection_RAM_" + datetime + ".bin", Injection);
                    File.WriteAllBytes(SaveDirectory + @"\" + VIN + "_" + vers + "_" + "Ignition_RAM_" + datetime + ".bin", Ignition);
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
                        statusTextBlock.Text = "Failed to read data. Please try again";
                    });
                }
                
            }
        }

        private async Task ReadDME_Data()
        {
            uint InjectionEnd = 0;
            uint IgnitionEnd = 0;
            byte[] Injection = new byte[0];
            byte[] Ignition = new byte[0];
            string ReadingText = String.Empty;

            byte[] InjectionEndBytes = { };
            byte[] IgnitionEndBytes = { };

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {

                ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch");

                RequestSecurityAccess(ediabas);
            }

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                DisableButtons(this, null);



                await Task.Run(() => InjectionEndBytes = ReadMemory(ediabas, 0x7001C, 0x7001F, String.Empty));
                await Task.Run(() => IgnitionEndBytes = ReadMemory(ediabas, 0x87001C, 0x87001F, String.Empty));

                InjectionEnd = BitConverter.ToUInt32(InjectionEndBytes.Reverse().ToArray(), 0) + 16;
                if (InjectionEnd < 0x70000 || InjectionEnd >= 0x7FFFC)
                    InjectionEnd = 0x7FFFF;
                ReadingText = "Reading Injection Tune";
                await Task.Run(() => Injection = ReadMemory(ediabas, 0x70000, InjectionEnd, ReadingText));
                if (InjectionEnd < 0x7FFFC)
                {
                    byte[] Checksum = { };
                    int length = Injection.Length;
                    Array.Resize(ref Injection, 0xFFFC);
                    for (int i = length; i < 0xFFFC; ++i)
                        Injection[i] = 0xFF;

                    await Task.Run(() => Checksum = ReadMemory(ediabas, 0x7FFFC, 0x7FFFF, ReadingText));
                    Injection = Injection.Concat(Checksum).ToArray();
                }

                IgnitionEnd = BitConverter.ToUInt32(IgnitionEndBytes.Reverse().ToArray(), 0) + 0x800000 + 16;
                if (IgnitionEnd < 0x870000 || IgnitionEnd >= 0x87FFFC)
                    IgnitionEnd = 0x87FFFF;
                ReadingText = "Reading Ignition Tune";
                await Task.Run(() => Ignition = ReadMemory(ediabas, 0x870000, IgnitionEnd, ReadingText));
                if (IgnitionEnd < 0x87FFFC)
                {
                    byte[] Checksum = { };
                    int length = Ignition.Length;
                    Array.Resize(ref Ignition, 0xFFFC);
                    for (int i = length; i < 0xFFFC; ++i)
                        Ignition[i] = 0xFF;

                    await Task.Run(() => Checksum = ReadMemory(ediabas, 0x87FFFC, 0x87FFFF, ReadingText));
                    Ignition = Ignition.Concat(Checksum).ToArray();
                }

                ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty);
            }
                ReenableButtons(this, null);

                byte[] DumpedTune = Injection.Concat(Ignition).ToArray();

                if (DumpedTune.Length == 0x20000)
                {
                    String VIN = Global.VIN;
                    String vers = Global.ZIF;
                    if (vers[0] == 'Z')
                        vers = vers.Substring(1) + "(v" + Global.Prog_Vers_internal_uint.ToString() + ")";

                    string datetime = DateTime.Now.ToString("yyyyMMddHHmm");

                    if (VIN == "")
                    {
                        VIN = "AA00000";
                    }
                    DirectoryInfo SaveDirectory = Directory.CreateDirectory(VIN);
                    File.WriteAllBytes(SaveDirectory + @"\" + VIN + "_" + vers + "_Tune_" + datetime + ".bin", Injection.Concat(Ignition).ToArray());

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
                        statusTextBlock.Text = "Failed to read data. Please try again";
                    });
                }
            }
       
        private async Task ReadDME_Full(bool QuickRead = true)
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

            bool BDMFormat = false;
            bool.TryParse(ConfigurationManager.AppSettings["ReadBDMFormat"], out BDMFormat);

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {

                ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch");

                RequestSecurityAccess(ediabas);
            }

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                DisableButtons(this, null);
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
                    //Console.WriteLine(end);
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
                    //Console.WriteLine(end);

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
                ReenableButtons(this, null);
                ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty);
            }

            byte[] dumpedFlash = Injection.Concat(Injection_ext.Concat(Ignition.Concat(Ignition_ext))).ToArray();
            ReenableButtons(this, null);
            if (dumpedFlash.Length == 0x500000)
            {
                String VIN = Global.VIN;
                string vers = Global.ZIF;

                if (vers[0] == 'Z')
                    vers = vers.Substring(1) + "_v" + Global.Prog_Vers_internal_uint.ToString();

                if (VIN == "")
                {
                    VIN = "AA00000";
                }
                string datetime = DateTime.Now.ToString("yyyyMMddHHmm");

                DirectoryInfo SaveDirectory = Directory.CreateDirectory(VIN);
                File.WriteAllBytes(SaveDirectory + @"\" + VIN + "_" + vers + "_Full_" + datetime + ".bin", dumpedFlash);
                File.WriteAllBytes(SaveDirectory + @"\" + VIN + "_" + vers + "_Tune_" + datetime + ".bin", Injection.Skip(0x70000).Take(0x10000).Concat(Ignition.Skip(0x70000).Take(0x10000)).ToArray());
                if (BDMFormat)
                {
                    byte[] Shadow = new byte[0x200];

                    if (Global.HW_Ref == "0569QT0")
                    {
                        Shadow[0] = 0x20;
                        Shadow[1] = 0x41;
                    }
                    if (Global.HW_Ref == "0569Q60")
                        Shadow[1] = 1;/*Technically this is not the true factory shadow section. 
                                       * But there's no harm and should reduce the risk of accidentally permalocking the MCU for anyone playing with the censor
                                       */
                    
                    for (int i = 4; i < 0x200; ++i)
                        Shadow[i] = 0xFF;

                    DirectoryInfo BDMSubdirectory = Directory.CreateDirectory(SaveDirectory + @"\" + VIN+ "_" + vers + "_BDM_" + datetime);
                    File.WriteAllBytes(BDMSubdirectory.FullName + @"\Inj_MPC.bin", Injection);
                    File.WriteAllBytes(BDMSubdirectory.FullName + @"\Inj_External.bin", Injection_ext);
                    File.WriteAllBytes(BDMSubdirectory.FullName + @"\Ign_MPC.bin", Ignition);
                    File.WriteAllBytes(BDMSubdirectory.FullName + @"\Ign_External.bin", Ignition_ext);
                    File.WriteAllBytes(BDMSubdirectory.FullName + @"\Shadow.bin", Shadow);
                }
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
                    statusTextBlock.Text = "Failed to read data. Please try again";
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
                uint start = 0x3f8000;
                uint end = 0x3fffff;
                byte[] InjRAMDump = new byte[0];
                byte[] ISN = new byte[0];
                byte[] ProtectedRead = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

                byte[] EWS4_SK_Header = { 0xA5, 0x00, 0xFF, 0xAA, 0xFF, 0xFF, 0xFF, 0xFF };
                byte[] EWS4_SK = new byte[0];

                using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
                {
                    DisableButtons(this, null);
                    String ReadingText = String.Empty;
                    if (Global.HW_Ref == "0569Q60")
                    {
                        ReadingText = String.Empty;
                        await Task.Run(() => ISN = ReadMemory(ediabas, 0x7940, 0x7945, ReadingText));
                        if (ISN.SequenceEqual(ProtectedRead))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                statusTextBlock.Text = "ISN is empty or protected. Please flash RSA bypass to read ISN";
                            });
                            ReenableButtons(this, null);
                            return;
                        }
                        byte[] CASISN = { ISN[2], ISN[1] };
                        this.Dispatcher.Invoke(() =>
                        {
                            statusTextBlock.Text = "CAS ISN: " + BitConverter.ToString(CASISN) + "\nDME ISN: " + BitConverter.ToString(ISN);                           
                        });

                        String VIN = Global.VIN;
                        if (VIN == "")
                        {
                            VIN = "AA00000";
                        }

                        DirectoryInfo SaveDirectory = Directory.CreateDirectory(VIN);
                        SaveFileDialog saveFile = new SaveFileDialog();
                        {
                            saveFile.FileName = "ISN";
                            saveFile.InitialDirectory = SaveDirectory.FullName;
                            //Console.WriteLine(saveFile.InitialDirectory);
                            saveFile.Filter = "Binary|*.bin|Original File|*.ori|All Files|*.*";
                            try
                            {
                                Nullable<bool> result = saveFile.ShowDialog();
                                if (result == true)
                                    File.WriteAllBytes(saveFile.FileName, ISN);
                            }
                            catch (Exception)
                            {
                                //Console.WriteLine("Exception caught in process: {0}", ex);
                                MessageBox.Show("Error trying to save file");
                            }

                        }
                    }

                    if (Global.HW_Ref == "0569QT0")
                    {
                        ReadingText = String.Empty;
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
                            /* This seems to work on most MSS60 variants from 060E onwards. Does not work on prototye v530 -- because it's still using EWS3
                             * Some reports of the method failing on 060E and 080E, but I haven't encountered that. 
                             * Perhaps earlier software versions cleared from RAM after authentication succeeded? (which wouldn't happen on the bench)
                             */

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
                                ReenableButtons(this, null);
                                return;
                            }

                            else
                            {
                                byte[] SK = InjRAMDump.Skip(IndexOfSK).Take(0x30).ToArray();
                                this.Dispatcher.Invoke(() =>
                                {
                                    statusTextBlock.Text = "Secret Key: " + BitConverter.ToString(SK.Take(0x10).ToArray());
                                    //Console.WriteLine("Secret Key Found @" + (IndexOfSK.ToString("x")));
                                });
                                EWS4_SK = EWS4_SK_Header.Concat(SK).ToArray();
                            }


                        }
                        String VIN = Global.VIN;
                        if (VIN == "")
                        {
                            VIN = "AA00000";
                        }
                        DirectoryInfo SaveDirectory = Directory.CreateDirectory(VIN);
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
                            catch (Exception)
                            {
                                //Console.WriteLine("Exception caught in process: {0}", ex);
                                MessageBox.Show("Error trying to save file");
                            }
                        }
                    }
                    ReenableButtons(this, null);
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
            if (start < 0x800000)
                segLength = 0x64;
            uint bytesRead = 0;

            //Console.WriteLine(Start.ToString("x"));

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

        private bool[] IsRSASegmentReadable_Bypassed(EdiabasNet ediabas)
        {
            bool[] RSABypassStatus = { true, true }; //RSAbypassed[0] == Readable, [1] = Bypassed
            uint RSASegmentsLocation = 0x10204;

            byte[] StockRSASegments = 
            {
                0x00, 0x00, 0x00, 0x05, 0x00, 0x07, 0x00, 0x00, 0x00, 0x07, 0x00, 0x3F, 0x00, 0x07, 0x01, 0xC0,
                0x00, 0x07, 0xFF, 0xFE, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0xFF, 0xFF, 0x00, 0x04, 0x00, 0x00,
                0x00, 0x06, 0xFF, 0xFF, 0x00, 0x45, 0x00, 0x00, 0x00, 0x5F, 0xFF, 0xFF,
            };

            byte[] FFs =
            {    
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            };

            byte[] zeroes =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            byte[] rsa_segments_injection = ReadMemory(ediabas, RSASegmentsLocation, RSASegmentsLocation + 0x2C - 1, String.Empty);
            byte[] rsa_segments_ignition = ReadMemory(ediabas, RSASegmentsLocation + 0x800000, RSASegmentsLocation + 0x800000 + 0x2C - 1, String.Empty);

            if (rsa_segments_injection.Length == 0 || rsa_segments_injection.SequenceEqual(FFs) || rsa_segments_injection.SequenceEqual(zeroes) ||
                rsa_segments_ignition.Length == 0 || rsa_segments_ignition.SequenceEqual(FFs) || rsa_segments_ignition.SequenceEqual(zeroes))
            {
                RSABypassStatus[0] = false;
            }


            if (StockRSASegments.SequenceEqual(rsa_segments_injection) || StockRSASegments.SequenceEqual(rsa_segments_ignition))
                RSABypassStatus[1] = false;


            return RSABypassStatus;
        }

        private bool IsPsuedoRSABypassPresent(byte[] injection, byte[] ignition)
        {
            bool PseudoRSABypassPresent = false;

            byte[] PseudoRSABypassSegments =
            {
                0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x3F, 0x00, 0x01, 0x01, 0xC0,
                0x00, 0x01, 0xFF, 0xFE, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0xFF, 0xFF, 0x00, 0x04, 0x00, 0x00,
                0x00, 0x06, 0xFF, 0xFF, 0x00, 0x45, 0x00, 0x00, 0x00, 0x5F, 0xFF, 0xFF,
            };
            if (injection.Skip(0x1C0).Take(0x2c).SequenceEqual(PseudoRSABypassSegments) && ignition.Skip(0x1C0).Take(0x2c).SequenceEqual(PseudoRSABypassSegments))
                PseudoRSABypassPresent = true;

            return PseudoRSABypassPresent;
        }

        private byte[] RemovePseudoRSABypass(byte[] binary)
        {
            byte[] stockRSASegments = 
            {
                0x00, 0x00, 0x00, 0x02, 0x00, 0x07, 0x00, 0x00, 0x00, 0x07, 0x00, 0x3F, 0x00, 0x07, 0x01, 0xC0,
                0x00, 0x07, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
            };
            for (int i = 0; i < stockRSASegments.Length; ++i)
                binary[0x1c0 + i] = stockRSASegments[i];

            return binary;
        }

        private async Task<bool> FlashDME_Data(byte[] tune)
        {
            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            
            bool success = true;
            bool IsSigValid =  ChecksumsSignatures.IsParamSignatureValid(tune);
            bool RSABypassInstalled = false;
            bool RSABypassReadable = false;
            bool affe0815_bypass = false;
            bool pseudoRSABypass = false;
            bool skipchecksums = false;
            byte[] affe0815_patch = new byte[64];
            string FlashingText = string.Empty;

            byte[] injection = tune.Take(0x10000).ToArray();
            byte[] ignition = tune.Skip(0x10000).Take(0x10000).ToArray();
            pseudoRSABypass = IsPsuedoRSABypassPresent(injection, ignition);
            



            //This hack to bypass RSA seems to work on any MSS65 and MSS60s 140E (657) and older.
            //Todo: Confirm whether there are any software variants between 140E and 170E, and if so if any of them can take advantage of this trick.
            //For now software newer than 140E / v657 will be assumed to require a true RSA bypass -- however I suspect anything older than v700 would work
            if ((Global.HW_Ref == "0569Q60" || (Global.HW_Ref == "0569QT0" && (Global.Prog_Vers_internal_uint <= 657))))
            {
                affe0815_bypass = true;
                
                affe0815_patch[0] = 0xaf;
                affe0815_patch[1] = 0xfe;
                affe0815_patch[2] = 0x08;
                affe0815_patch[3] = 0x15;
            }


            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                bool[] RSABypassArray = IsRSASegmentReadable_Bypassed(ediabas);
                RSABypassReadable = RSABypassArray[0];
                RSABypassInstalled = RSABypassArray[1];
            }

            if (!IsSigValid && !RSABypassInstalled && !affe0815_bypass && !pseudoRSABypass)
            {
                string msg = String.Empty;
                if (RSABypassReadable)
                {
                    msg = "Warning: We have detected you are trying to flash a non-stock tune. We cannot detect an RSA bypass. " +
                                    "Flashing this tune is likely to fail unless your RSA is bypassed by a different method (e.g BDM).\n\nWould you like to continue?";
                }
                else
                {
                    msg = "Warning: We have detected you are trying to flash a non-stock tune. We are unable to read the RSA segments from your DME. " +
                                    "This usually means your DME is read locked or a recent program flash failed. If you know your RSA is definitely bypassed, you may continue to flash.\n\nWould you like to continue?";
                }
                
                MessageBoxResult result =  MessageBox.Show(msg, "Unable to detect RSA Bypass",  MessageBoxButton.YesNo,  MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Tune write cancelled";
                    });
                    ReenableButtons(this, null);
                    return false;
                }
            }
            



            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"],  ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {
                DisableButtons(this, null);
                await Task.Run(() =>
                {
                    if (!ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch")) 
                    {
                        //Console.WriteLine("Failed to set DME Flash Parameters");
                    }
  
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
                uint eraseBlock = 0x1;
                uint flashStart = 0x70000;
                uint IgnitionOffset = 0x800000;


                if (pseudoRSABypass || IsSigValid)
                {
                    skipchecksums = true;
                }
                if (pseudoRSABypass && (affe0815_bypass || RSABypassInstalled))
                {
                    injection = RemovePseudoRSABypass(injection);
                    ignition = RemovePseudoRSABypass(ignition);
                    skipchecksums = false;
                }
                if (!skipchecksums)
                {
                    injection = ChecksumsSignatures.CorrectParameterChecksums(injection);
                    ignition = ChecksumsSignatures.CorrectParameterChecksums(ignition);
                }

                uint flashEndInj = BitConverter.ToUInt32(injection.Skip(0x1c).Take(4).Reverse().ToArray(), 0) + 16; //taking extra bytes in case parameters go a little past (like some MSS65)
                if (flashEndInj < 0x70000 || flashEndInj >= 0x7FFFC)
                    flashEndInj = 0x7FFFF;

                uint flashEndIgn = BitConverter.ToUInt32(ignition.Skip(0x1c).Take(4).Reverse().ToArray(), 0) + 16;
                if (flashEndIgn < 0x70000 || flashEndIgn >= 0x7FFFC)
                    flashEndIgn = 0x7FFFF;


                if (!ExecuteJob(ediabas, "normaler_datenverkehr", "nein;nein;ja"))
                    return false;
                if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;nein"))
                    return false;



                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Erasing Flash";
                });
                await Task.Run(() => success = EraseECU(ediabas, eraseBlock, eraseStart));

                if (success)
                {
                    FlashingText = "Injection: Flashing Tune";
                    await Task.Run(() => success = FlashBlock(ediabas, injection.Take(0x80).ToArray(), flashStart, flashStart + 0x7F, FlashingText)); //0x70080 -> 0x700BF is protected on MSS60.
                }

                if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, injection.Skip(0xc0).ToArray(), flashStart + 0xc0, flashEndInj, FlashingText));
                if (flashEndInj < 0x7FFFC)
                {
                    if (success)
                        await Task.Run(() => success = FlashBlock(ediabas, injection.Skip(0xFFFC).ToArray(), 0x7FFFC, 0x7FFFF, FlashingText)); // write checksum
                }


                if (success)
                {
                    FlashingText = "Ignition: Flashing Tune";
                    await Task.Run(() => success = FlashBlock(ediabas, ignition.Take(0x80).ToArray(), flashStart + IgnitionOffset, flashStart + 0x7F + IgnitionOffset, FlashingText));
                }
                if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, ignition.Skip(0xc0).ToArray(), flashStart + 0xc0 + IgnitionOffset, flashEndIgn + IgnitionOffset, FlashingText));
                if (flashEndIgn < 0x7FFFC)
                { 
                    if (success)
                    await Task.Run(() => success = FlashBlock(ediabas, ignition.Skip(0xFFFC).ToArray(), 0x87FFFC, 0x87FFFF, FlashingText));
                }

                if (affe0815_bypass)
                {
                    if (success)
                    {
                        FlashingText = String.Empty;
                        await Task.Run(() => success = FlashBlock(ediabas, affe0815_patch, flashStart + 0x80, flashStart + 0xBF, FlashingText));
                    }
                    if (success)
                    {
                        await Task.Run(() => success = FlashBlock(ediabas, affe0815_patch, flashStart + 0x80 + IgnitionOffset, flashStart + 0xBF + IgnitionOffset, FlashingText));
                    }
                }


                if (success)
                {
                    await Task.Run(() =>
                    {
                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;ja"))
                        {
                            success = false;
                            ReenableButtons(this, null);
                            return;
                        }


                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))
                        {
                            success = false;
                            ReenableButtons(this, null);
                            return;
                        }
                        
                        if (!affe0815_bypass) 
                                                        //If we're writing the "RSA Passed" bytes directly, no need to do a signature check.
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
                                //Console.WriteLine(GetResult_String("JOB_STATUS", ediabas.ResultSets));
                                success = false;
                                ReenableButtons(this, null);
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
                        ReenableButtons(this, null);
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
                            statusTextBlock.Text = "Flash success";
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
                ReenableButtons(this, null);
                return success;
            }
            
        }

        private async Task<bool> FlashDME_Program(byte[] full, bool bypassRSA_stage1 = false, bool bypassRSA_stage2 = false, bool restoreStock = false)
        {

            Checksums_Signatures ChecksumsSignatures = new Checksums_Signatures();
            bool success = true;
            String FlashingText = String.Empty;

            bool SigValid = ChecksumsSignatures.IsProgramSignatureValid(full);
            bool RSABypassInstalled = false;
            bool RSABypassReadable = false;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultSGBD"]))
            {
                bool[] RSABypassArray = IsRSASegmentReadable_Bypassed(ediabas);
                RSABypassReadable = RSABypassArray[0];
                RSABypassInstalled = RSABypassArray[1];
            }
            if (!RSABypassReadable && !SigValid)
            {
                string msg = "" +
                    "You are trying to flash a non-stock program and we are unable to detect if you have an RSA Bypass installed. This can happen if your DME is read locked or if your most recent program write failed. "
                    + "If you know you have an RSA bypass, you may continue to flash\n\nWould you like to Continue?";
                MessageBoxResult result = MessageBox.Show(msg, "RSA Bypass Not Readable", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Program flash cancelled";
                    });
                    ReenableButtons(this, null);
                    return false;
                }
                else
                    RSABypassInstalled = true;
            }
            if (!RSABypassReadable && SigValid)
            {
                string msg = "We cannot read your RSA bypass status. This can happen if your DME is read locked or if your most recent program write failed. "
                    + "If you know you have an RSA bypass, we can patch the loaded file to maintain the bypass.\n\nShould we assume RSA is already bypassed?";
                MessageBoxResult result = MessageBox.Show(msg, "RSA Bypass Not Readable", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    RSABypassInstalled = false;
                }
                else
                    RSABypassInstalled = true;
            }
            

            if (!SigValid && !RSABypassInstalled && !bypassRSA_stage1)
            {
                string msg = "We detected you are trying to flash a non-stock program. We cannot detect an RSA bypass. " +
                                "This is likely to fail unless you know your Program RSA check is bypassed some other way (e.g BDM).\n\nWould you like to continue?";
                MessageBoxResult result =  MessageBox.Show(msg, "RSA Bypass Not Installed", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    RSABypassInstalled = false;
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Program flash cancelled";
                    });
                    ReenableButtons(this, null);
                    return false;
                }
                RSABypassInstalled = true;
            }


            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"],  ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {
                DisableButtons(this, null);
                uint flashStart_Section1 = 0x10000;
                uint flashEnd_Section1 = 0x1FFFF;

                uint ignitionOffset = 0x800000;

                byte[] injection = full.Take(0x280000).ToArray();
                byte[] ignition = full.Skip(0x280000).Take(0x280000).ToArray();
                

                if (!(SigValid && (!RSABypassInstalled || restoreStock)) || bypassRSA_stage1)
                {
                    injection = ChecksumsSignatures.PatchProgram(injection);
                    ignition = ChecksumsSignatures.PatchProgram(ignition);
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
                        //Console.WriteLine("Failed to set DME flash parameters");
                    }

                   /* ExecuteJob(ediabas, "aif_lesen", string.Empty);

                    ExecuteJob(ediabas, "hardware_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "daten_referenz_lesen", string.Empty);

                    ExecuteJob(ediabas, "flash_programmier_status_lesen", string.Empty);

                    ExecuteJob(ediabas, "FLASH_ZEITEN_LESEN", string.Empty);*/

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
                uint eraseBlock = 0x1;

                uint eraseStartRSA = 0x70000;
                uint eraseBlockRSA = 0x1;

                uint flashStart_Section2 = 0x20000;
                uint flashEnd_Section2 = 0x6FFFF;

                uint flashExtStart = 0x450000;
                uint flashExtEnd_Inj = BitConverter.ToUInt32(injection.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7;
                uint flashExtEnd_Ign = BitConverter.ToUInt32(ignition.Skip(0x1001c).Take(4).Reverse().ToArray(), 0) + 7;

                if (flashExtEnd_Inj > 0x5FFFFF)
                    flashExtEnd_Inj = 0x5FFFFF;
                if (flashExtEnd_Ign > 0x5FFFFF)
                    flashExtEnd_Ign = 0x5FFFFF;

               // //Console.WriteLine(flashExtEnd_Inj.ToString("x"));
               // //Console.WriteLine(flashExtEnd_Ign.ToString("x"));

                await Task.Run(() =>
                    {

                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "nein;nein;ja"))
                        {
                            //Console.WriteLine("Failed to shut down vehicle electronics");
                            //success = false;
                        }

                        if (!ExecuteJob(ediabas, "normaler_datenverkehr", "ja;nein;nein"))
                        {
                            //Console.WriteLine("Failed to shut down vehicle electronics");
                            //success = false;
                        }
                    });


                if (success)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = "Erasing ECU";
                    });

                    if (bypassRSA_stage1)
                    {
                        await Task.Run(() => success = EraseECU(ediabas, eraseBlockRSA, eraseStartRSA));
                    }
                    else
                    {
                        await Task.Run(() => success = EraseECU(ediabas, eraseBlock, eraseStart));
                    }
                }
                if (!bypassRSA_stage2)
                {
                    if (success)
                    {
                        if (bypassRSA_stage1)
                            FlashingText = "Injection: Flashing RSA Bypass";
                        else
                            FlashingText = "Injection: Flashing Program Section 1";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect1.Take(0x80).ToArray(), flashStart_Section1, flashStart_Section1 + 0x7F, FlashingText));
                        //On MSS60, 0x10080 -> 0x100BF is protected
                    }

                    if (success)
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect1.Skip(0xc0).ToArray(), flashStart_Section1 + 0xc0, flashEnd_Section1, FlashingText));
                }

                if (success)
                {
                    if (!bypassRSA_stage1)
                    {
                        FlashingText = "Injection: Flashing Program Section 2";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjInt_Sect2, flashStart_Section2, flashEnd_Section2, FlashingText));
                        FlashingText = "Injection: Flashing Program Section 3";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_InjExt, flashExtStart, flashExtEnd_Inj, FlashingText));

                    }
                }

                if (!bypassRSA_stage2)
                {
                    if (success)
                    {
                        if (bypassRSA_stage1)
                            FlashingText = "Ignition: Flashing RSA Bypass";
                        else
                            FlashingText = "Ignition: Flashing Program Section 1";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect1.Take(0x80).ToArray(), flashStart_Section1 + ignitionOffset, flashStart_Section1 + 0x7F + ignitionOffset, FlashingText));
                    }
                    if (success)
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect1.Skip(0xc0).ToArray(), flashStart_Section1 + 0xc0 + ignitionOffset, flashEnd_Section1 + ignitionOffset, FlashingText));
                }

                if (!bypassRSA_stage1)
                {
                    if (success)
                    {
                        FlashingText = "Ignition: Flashing Program Section 2";
                        await Task.Run(() => success = FlashBlock(ediabas, toFlash_IgnInt_Sect2, flashStart_Section2 + ignitionOffset, flashEnd_Section2 + ignitionOffset, FlashingText));
                    }

                    if (success)
                    {
                        FlashingText = "Ignition: Flashing Program Section 3";
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
                            ReenableButtons(this, null);
                            return;
                        }

                        if (!ExecuteJob(ediabas, "FLASH_PROGRAMMIER_STATUS_LESEN", String.Empty))

                        {
                            success = false;
                            ReenableButtons(this, null);
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
                            ReenableButtons(this, null);
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
   
                        if (!bypassRSA_stage1)
                            ReenableButtons(this, null);
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
                            UpdateProgressBar(0);

                            if (bypassRSA_stage1)
                                statusTextBlock.Text = "RSA Bypass accepted. Preparing DME for program";
                            else
                                statusTextBlock.Text = "Program flash success. Flash tune to finish";
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
                if (!bypassRSA_stage1)
                {
                    byte[] tune = full.Skip(0x70000).Take(0x10000).Concat(full.Skip(0x2F0000).Take(0x10000)).ToArray();
                    if (success)
                    {
                        await Task.Run(() => success = FlashDME_Data(tune).Result);
                    }
                    ReenableButtons(this, null);
                    if (!bypassRSA_stage2)
                    {
                        LoadFile_Checks(full);
                    }
                }
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
            int flashSegLength = 0xFC;
            flashHeader[0] = 1;
            flashHeader[13] = (byte)flashSegLength;

            string flashAddressJob = "flash_schreiben_adresse";
            string flashJob = "flash_schreiben";
            string flashEndJob = "flash_schreiben_ende";

            this.Dispatcher.Invoke(() =>
            {
                programStatus_Box.Content = "Programming session active";
            });
            

            if (!ExecuteJob(ediabas, flashAddressJob, flashAddressSet))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Failed to set flash address @ 0x" +blockStart.ToString("x");
                    //Console.WriteLine("Flash Address Message: " + BitConverter.ToString(GetResult_ByteArray("_TEL_AUFTRAG", ediabas.ResultSets)));
                });
                ReenableButtons(this, null);
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                return false;
            }

            while (blockLength > 0)
            {
                if (blockLength < flashSegLength)
                {
                    flashSegLength = (int)blockLength;
                    flashHeader[13] = (byte)flashSegLength;
                }
                BitConverter.GetBytes(blockStart).CopyTo(flashHeader, 17);

                if (FlashingText != String.Empty)
                {
                    this.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        statusTextBlock.Text = FlashingText + " @ 0x" + blockStart.ToString("x");
                    }));
                }

                if (!ExecuteJob(ediabas, flashJob, flashHeader.Concat(toFlash.Skip((int)(blockStart) - (int)blockStartOrig).Take(flashSegLength)).Concat(three).ToArray())) //See Ediabas comments for details on what the flash message should look like
                {
                    ReenableButtons(this, null);
                    this.Dispatcher.Invoke(() =>
                    {
                        //Console.WriteLine("Flash failed at 0x" + blockStart.ToString("X") + ". Resetting DME.");
                        statusTextBlock.Text = "Flash failed at 0x" + blockStart.ToString("X") + ". Resetting DME.";
                    });
                    if (!ExecuteJob(ediabas, "STEUERGERAETE_RESET", String.Empty))
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            //Console.WriteLine("Error Resetting ECU");
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
                ReenableButtons(this, null);
                return false;
            }
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS); //Allow system to idle
            return true;
        }

        private bool EraseECU(EdiabasNet ediabas, uint blockLength, uint blockStart)
        {
            string flashEraseJob = "flash_loeschen";
            byte[] eraseCommand = new Byte[22];
            eraseCommand[0] = 1;
            eraseCommand[1] = 1;
            eraseCommand[4] = 0xFA;
            eraseCommand[9] = 0xFF;

            BitConverter.GetBytes(blockStart).CopyTo(eraseCommand, 17); //Start address
            BitConverter.GetBytes(blockLength).CopyTo(eraseCommand, 13); //Length - doesn't really matter for erases. 
                                                                         //Erasing anything in the program space will erase the entire program space for both CPUs
                                                                         //Erasing anything in the parameter space will erase entire parameter space for both CPUs
            DisableButtons(this, null);
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = true;
                programStatus_Box.Content = String.Empty;
            });
            if (!ExecuteJob(ediabas, flashEraseJob, eraseCommand))
            {
                this.Dispatcher.Invoke(() =>
                {
                    statusTextBlock.Text = "Erase failed";
                });
                ReenableButtons(this, null);
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            //ReenableButtons(this, null);
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = false;
            });
            return true;
        }

        private bool RequestSecurityAccess(EdiabasNet ediabas, byte securityAccessLevel = 3, bool fromSecurityAccessMenuButton = false)
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
            rng.NextBytes(userID);

            if (!ExecuteJob(ediabas, "authentisierung_zufallszahl_lesen", securityAccessLevel.ToString() + ";0x" + BitConverter.ToUInt32(userID.Reverse().ToArray(), 0).ToString("X")))//Request random number, passing the "userID" generated above as an argument
            {
                Console.WriteLine("Failed to get random number");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            byte[] seed = GetResult_ByteArray("ZUFALLSZAHL", ediabas.ResultSets); //DME sends a random number

            //Console.WriteLine(BitConverter.ToString(userID.Concat(serialNumber.Concat(seed)).ToArray()));           
            if (!ExecuteJob(ediabas, "authentisierung_start", ChecksumsSignatures.GetSecurityAccessMessage(userID, serialNumber, seed, securityAccessLevel))) //Sign message using private key. If DME decrypts successfully and it matches its own calculation, security access is granted
            {
                Console.WriteLine("Failed to authenticate tester");
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;

                });
                return false;
            }
            if (!fromSecurityAccessMenuButton)
            {
                if (!ExecuteJob(ediabas, "diagnose_mode", "ECUPM"))
                {
                    Console.WriteLine("Could Not Request ECUPM");
                    this.Dispatcher.Invoke(() =>
                    {
                        ProgressDME.IsIndeterminate = false;
                    });
                    return false;
                }
            }
            this.Dispatcher.Invoke(() =>
            {
                ProgressDME.IsIndeterminate = false;
            });

            /*
             * Seems like need to send tester present message periodically to keep the car electronics disabled. For now not implemented.
             * Will likely need to create a separate function and call it every x seconds while reading, programming etc
             * Will test further when I get a real M5

            if (!ExecuteJob(ediabas, "diagnose_aufrecht", "1;0"))
            {
                this.Dispatcher.Invoke(() =>
                {
                    ProgressDME.IsIndeterminate = false;
                });
                return false;
            }
            */
                    
            return true;
            //Should be in ECU Programming Mode now
        }

        //The below is basically ripped straight out of example ediabaslib code
        private EdiabasNet StartEdiabas(string port, string path, string sgbd)
        {
            EdiabasNet ediabas = new EdiabasNet();
            EdInterfaceBase edInterface = null;

            /* Thus far unable to get ICOM to work -- might only work with newer (ENET) cars as far as EdiabasLib goes
             * Tried making my own EdiabasLib bluetooth adapter from an ELM327, but the thing ended up bricked
             * The only places that sell a premade one don't ship to the US due to the unpredictable tariffs
             * So for now, only the K-line and K+D-Can cables are supported. Unfortunately speed for these are limited by the 115200bps baudrate. Have not been successful in increasing this
             * Supporting a native CAN interface (perhaps building off CandleLight project) could be great, but beyond the scope of this project. And probably a decade too late
             * Perhaps we can take ENET commands and translate them to a CAN interface? 
             */

            /*string ifhName = String.Empty;
            if (string.IsNullOrEmpty(ifhName))
            {
                ifhName = ediabas.GetConfigProperty("Interface");
            }


            if (!string.IsNullOrEmpty(ifhName))
            {
                if (EdInterfaceObd.IsValidInterfaceNameStatic(ifhName))
                {
                    edInterface = new EdInterfaceObd();
                }
                else if (EdInterfaceEdic.IsValidInterfaceNameStatic(ifhName))
                {
                    edInterface = new EdInterfaceEdic();
                }
                else if (EdInterfaceRplus.IsValidInterfaceNameStatic(ifhName))
                {
                    edInterface = new EdInterfaceRplus();
                }
                else
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        statusTextBlock.Text = ("Interface not valid");
                    });
                    
                }
            }
            else
            {
                edInterface = new EdInterfaceObd();
            }
            edInterface.IfhName = ifhName;
            */

            edInterface = new EdInterfaceObd();
            edInterface.ApplicationName = "MSS6x Flasher";

            ediabas.EdInterfaceClass = edInterface;
            ediabas.ProgressJobFunc = ProgressJobFunc;
            ediabas.ErrorRaisedFunc = ErrorRaisedFunc;
            if (!string.IsNullOrEmpty(port))
            {
                if (edInterface is EdInterfaceObd)
                {
                    ((EdInterfaceObd)edInterface).ComPort = port;
                }
                /*else if (edInterface is EdInterfaceEdic)
                {
                    ((EdInterfaceEdic)edInterface).ComPort = port;
                }*/
            }
            else
            {
                ((EdInterfaceObd)edInterface).ComPort = "COM1";
            }


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

        private void IdentDME_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar(0);
            ClearText_DisableButtons();
            IdentDME();
        }

        private async void ReadTune_Click(object sender, RoutedEventArgs e)
        {
            await ReadDME_Data();
        }

        private async void ReadFull_Click(object sender, RoutedEventArgs e)
        {
            await ReadDME_Full(true);
        }

        private async void Read_Full_Long_Click(object sender, RoutedEventArgs e)
        {
            await ReadDME_Full(false);
        }

        private async void Read_ISN_SK_Click(object sender, RoutedEventArgs e)
        {
            await ReadISN_SK();
        }

        private async void Read_RAM_Click(object sender, RoutedEventArgs e)
        {
            await ReadRAM();
        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar(0);
            LoadFile_Dialog();
        }

        private async void FlashData_Click(object sender, RoutedEventArgs e)
        {
            byte[] tune;
            if (IsFullBinLoaded() || Global.openedFlash.Length > 0x20000)
                tune = Global.openedFlash.Skip(0x70000).Take(0x10000).Concat(Global.openedFlash.Skip(0x2F0000).Take(0x10000)).ToArray();
            else
                tune = Global.openedFlash;

            await FlashDME_Data(tune);
        }

        private async void FlashProgram_Click(object sender, RoutedEventArgs e)
        {
            await FlashDME_Program(Global.openedFlash);
        }
        private async void RestoreStock_Click(object sender, RoutedEventArgs e)
        {
            await FlashDME_Program(Global.openedFlash,restoreStock:true);
        }
        private async void FlashRSA_Bypass_Click(object sender, RoutedEventArgs e)
        {
            await RSABypassTasks();
        }



        private void SecurityAccessClick(byte level)
        {
            bool success = false;

            string mode = String.Empty;

            using (EdiabasNet ediabas = StartEdiabas(ConfigurationManager.AppSettings["Port"], ConfigurationManager.AppSettings["ecuPath"], ConfigurationManager.AppSettings["defaultProgrammingSGBD"]))
            {
                if (ExecuteJob(ediabas, "FLASH_PARAMETER_SETZEN", "0x12;64;64;254;Asymetrisch"))
                {
                    success = RequestSecurityAccess(ediabas, level, true);
                }
            }
            if (success)
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    statusTextBlock.Text = "Security Access Level " + level.ToString() + " Granted";
                }));
            }
            else
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    statusTextBlock.Text = "Security Access Denied";
                }));
            }
        }
        private void Level_3_Click(object sender, RoutedEventArgs e)
        {
            SecurityAccessClick(3);
        }
        private void Level_4_Click(object sender, RoutedEventArgs e)
        {
            SecurityAccessClick(4);
        }
        private void Level_5_Click(object sender, RoutedEventArgs e)
        {
            SecurityAccessClick(5);
        }
        private void AppExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Reset_DME_Click (object sender, RoutedEventArgs e)
        {
            ResetDME();
        }

        private void Url_Click(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

    }
}


