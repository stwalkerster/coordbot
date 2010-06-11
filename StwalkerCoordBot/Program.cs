// <copyright file="Program.cs" company="Simon Walker">
// This work is licensed under the Creative Commons Attribution-Share Alike 3.0 Unported License. To view a copy of this license, visit http://creativecommons.org/licenses/by-sa/3.0/ or send a letter to Creative Commons, 171 Second Street, Suite 300, San Francisco, California, 94105, USA.
// </copyright>

namespace StwalkerCoordBot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.XPath;

    /// <summary>
    /// Main bot class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Template for the bot to use.
        /// </summary>
        private static string format = "{{{{coord|{0}|N|{1}|E|display=inline,title}}}}";

        /// <summary>
        /// Email address to send the report to.
        /// </summary>
        private static string reportEmail = "stwalkerster@helpmebot.org.uk";

        /// <summary>
        /// Main method, initialises the bot
        /// </summary>
        /// <param name="args">Program arguments passed to the executable</param>
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                PrintHelp();
                return;
            }

            reportEmail = args[1];

            FileInfo fi = new FileInfo(args[0]);
            if (fi.Extension == ".kml")
            {
                RunBot(GetLocations(args[0]));
            }
        }

        /// <summary>
        /// Prints out useage help if invalid params are given
        /// </summary>
        private static void PrintHelp()
        {
            Console.WriteLine("Usage: mono coordbot.exe <kml file> <report email>");
        }

        /// <summary>
        /// Main bot method that executes everything needed
        /// </summary>
        /// <param name="locations">Dictionary of locations, generated from the kml by getLocations sub</param>
        private static void RunBot(Dictionary<string, Location> locations)
        {
            if (locations == null)
            {
                return;
            }

            string report = "StwalkerCoordBot report:\n";

            foreach (KeyValuePair<string, Location> locationData in locations)
            {
                ////string.Format(format, locationData.Value.Latitude, locationData.Value.Longitude);

                report += "[[" + locationData.Key + "]]: " + string.Format(format, locationData.Value.Latitude, locationData.Value.Longitude) + "\n";
            }

            SendReport(report);
        }

        /// <summary>
        /// Sends a report to the operator of the bot
        /// </summary>
        /// <param name="report">the report to be sent</param>
        private static void SendReport(string report)
        {
            System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage("stwalkercoordbot@helpmebot.org.uk", reportEmail);
            mail.Body = report;
            mail.Subject = "StwalkerCoordBot report";
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("helpmebot.org.uk");
            smtp.Send(mail);
        }

        /// <summary>
        /// Parses the kml file
        /// </summary>
        /// <param name="filePath">Path of the KML file</param>
        /// <returns>a dictionary of required edits</returns>
        private static Dictionary<string, Location> GetLocations(string filePath)
        {
            Dictionary<string, Location> locs = new Dictionary<string, Location>();

            XPathDocument xpd = new XPathDocument(filePath);
            XPathNavigator xpn = xpd.CreateNavigator();
            XmlNamespaceManager xnm = new XmlNamespaceManager(xpn.NameTable);
            xnm.AddNamespace("gx", "http://www.google.com/kml/ext/2.2");
            xnm.AddNamespace("kml", "http://www.opengis.net/kml/2.2");
            XPathExpression xpath = XPathExpression.Compile("//kml:Placemark", xnm);
            XPathNodeIterator xpni = xpn.Select(xpath);
            bool first = true;
            while (xpni.MoveNext())
            {
                first = false;
                string article = string.Empty;
                string coord = string.Empty;

                XmlReader xr = xpni.Current.ReadSubtree();

                while (xr.Read())
                {
                    if (xr.Name == "name")
                    {
                        article = xr.ReadElementContentAsString();
                    }

                    if (xr.Name == "Point")
                    {
                        xr.Read();
                        coord = xr.ReadElementContentAsString();
                    }
                }

                string[] cd = coord.Split(',');

                Location loc = new Location(float.Parse(cd[1]), float.Parse(cd[0]));

                locs.Add(article, loc);
            }

            return locs;
        }

        /// <summary>
        /// A location structure which holds the Lat/Long information
        /// </summary>
        private struct Location
        {
            /// <summary>
            /// The latitude of the location. Internal field, inaccessible
            /// </summary>
            private float latitude;

            /// <summary>
            /// The longitude of the location. Internal field, inaccessible
            /// </summary>
            private float longitude;

            /// <summary>
            /// Initializes a new instance of the Location struct.
            /// </summary>
            /// <param name="latitude">Latitude of the location</param>
            /// <param name="longitude">Longitude of the location</param>
            public Location(float latitude, float longitude)
            {
                this.latitude = latitude;
                this.longitude = longitude;
            }

            /// <summary>
            /// Gets the latitude of the location
            /// </summary>
            public float Latitude
            {
                get
                {
                    return this.latitude;
                }
            }

            /// <summary>
            /// Gets the longitude of the location
            /// </summary>
            public float Longitude
            {
                get
                {
                    return this.longitude;
                }
            }
        }
    }
}