using log4net;
using log4net.Config;
using log4net.Core;
using Microsoft.Extensions.Hosting;

namespace Benny_Scraper
{
    internal class Logger
    {
        public static readonly ILog Log = LogManager.GetLogger(typeof(Logger));

        public static void Setup()
        {
            if (!Directory.Exists(@"C:\logs"))
                Directory.CreateDirectory(@"C:\logs");
            var appender = new log4net.Appender.RollingFileAppender
            {
                File = @"C:\logs\mylog.txt",
                AppendToFile = true,
                RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Size,
                MaxSizeRollBackups = 5,
                MaximumFileSize = "1MB",
                StaticLogFileName = true,
                Layout = new log4net.Layout.PatternLayout("%date [%thread] %-5level %logger - %message%newline")
            };
            BasicConfigurator.Configure(appender);
            XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
        }      

    }
}
