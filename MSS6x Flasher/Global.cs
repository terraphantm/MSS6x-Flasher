using System;
using System.Configuration;
using System.Deployment.Application;
using System.Reflection;


namespace MSS6x_Flasher
{
    public class Global //I know global variables are supposed to be bad, but I'm not a good enough programmer to get around them 
    {
        public static string Title = SetTitle();
        public static string VIN;
        public static string HW_Ref;
        //public static string SW_Ref;
        public static string ZIF;
        /*public static string sgbd = ConfigurationManager.AppSettings["defaultSGBD"];
        public static string ecuPath = ConfigurationManager.AppSettings["ecuPath"];
        public static string Port = ConfigurationManager.AppSettings["Port"];*/


        public static byte[] openedFlash = null;

        private static string SetTitle()
        {
            Version version;
            string Title;
            try
            {
                version = ApplicationDeployment.CurrentDeployment.CurrentVersion;
            }
            catch (Exception)
            {
                version = Assembly.GetExecutingAssembly().GetName().Version;
            }
            Title = "MSS6x Flasher " + version;

            return Title;
        }
    }

}
