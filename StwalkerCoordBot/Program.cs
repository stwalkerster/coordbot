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
        /// Main method, initialises the bot
        /// </summary>
        /// <param name="args">Program arguments passed to the executable</param>
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                PrintHelp();
                return;
            }

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
            Console.WriteLine("Usage: mono coordbot.exe <kml file>");
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

            foreach (KeyValuePair<string, Location> locationData in locations)
            {
                Console.WriteLine("[[" + locationData.Key + "]]: " + locationData.Value.Latitude + " N, " + locationData.Value.Longitude + " E");
            }
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
            XPathNodeIterator xpni = xpn.Select("//Placemark");

            while (xpni.MoveNext())
            {
                string article = string.Empty;
                string coord = string.Empty;

                XmlReader xr = xpni.Current.ReadSubtree();
                while (xr.Read())
                {
                    if (xr.Name == "name")
                    {
                        article = xr.ReadContentAsString();
                    }

                    if (xr.Name == "Point")
                    {
                        xr.Read();
                        coord = xr.ReadContentAsString();
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