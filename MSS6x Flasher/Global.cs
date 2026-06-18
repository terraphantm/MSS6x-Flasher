using System;
using System.Configuration;
using System.Reflection;
namespace MSS6x_Flasher

{
    public class Global
    {
        public static string Title = SetTitle();
        public static string VIN;
        public static string HW_Ref;
        public static int Prog_Vers_internal_int;
        public static string ZIF;
        public static int programmingStatusByte = 0xFF;
        public static byte[] openedFlash = null;
        public static bool[] RsaBypassStatus = null;

        public static string[] programmingStatusStringArray =
        {
            "Delivery status","Normal operation",String.Empty,"Memory erased",String.Empty,
            "Program signature check failed or not done","Data signature check failed or not done",
            "Program-programming session active","Data-programming session active",
            "Hardware reference faulty", "Program reference faulty",
            "Referencing error Hardware -> Program", "Program not available or not complete",
            "Data reference faulty", "Referencing error Program -> Data","Data not available or not complete"
        };
    /*  0x00	Delivery status
    0x01	Normal operation
    0x03	Memory erased
    0x05	Signature check PAF not done
    0x06	Signature check DAF not done
    0x07	Program-programming session active
    0x08	Data-programming session active
    0x09	Hardware reference faulty
    0x0A	Program reference faulty
    0x0B	Referencing error Hardware -> Program
    0x0C	Program not available or not complete
    0x0D	Data reference faulty
    0x0E	Referencing error Program -> Data
    0x0F	Data not available or not complete
    */


        private static string SetTitle()
        {
            Version version;
            string Title;
            try
            {
                version = Assembly.GetExecutingAssembly().GetName().Version;
            }
            catch (Exception)
            {
                version = null;
            }
            Title = "MSS6x Flasher " + version;

            return Title;
        }

        private static string GetConfig(string config)
        {
           return ConfigurationManager.AppSettings[config];
        }
    }

}
