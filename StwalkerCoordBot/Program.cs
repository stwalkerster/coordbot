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
    using Utility.Net.MediaWiki;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Main bot class
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Template for the bot to use.
        /// </summary>
        private static string coordInlineTitle = "{{{{coord|{0}|N|{1}|E|display=inline,title}}}}";
        private static string coordInline = "{{{{coord|{0}|N|{1}|E|display=inline}}}}";
        private static string coordTitle = "{{{{coord|{0}|N|{1}|E|display=title}}}}";

        /// <summary>
        /// Email address to send the report to.
        /// </summary>
        private static string reportEmail = "stwalkerster@helpmebot.org.uk";
        private static string reportPage = "User:Stwalkerbot/Coordinates report";
        private static string report = "";

        private static bool silent = false;
        private static bool summaryEdit = true;
        private static bool doEdits = true;
        private static int editLimit = -1;
        private static int editCount = 0;


        private static string username;

        private static MediaWikiApi api;

        /// <summary>
        /// Main method, initialises the bot
        /// </summary>
        /// <param name="args">Program arguments passed to the executable</param>
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }

            api = new Utility.Net.MediaWiki.MediaWikiApi();

            SetOptions(args);

            Console.Write("Bot username: ");
            username = Console.ReadLine();
            Console.Write("Bot password: ");
            string password = Console.ReadLine();

            api.Login(username, password);

            FileInfo fi = new FileInfo(args[0]);
            if (fi.Extension == ".kml")
            {
                report += ":Using KML file: " + args[0] + "\n";

                RunBot(GetLocations(args[0]));
            }
        }

        private static void SetOptions(string[] args)
        {
            if (args.Contains("--silent"))
            {
                silent = true;
            }

            if (args.Contains("--nosummaryedit"))
                summaryEdit = false;

            if (args.Contains("--noedit"))
                doEdits = false;

            if (args.Contains("--editlimit"))
            {

            }
        }

        /// <summary>
        /// Prints out useage help if invalid params are given
        /// </summary>
        private static void PrintHelp()
        {
            Console.WriteLine("Usage: mono coordbot.exe <kml file> [options]");
            Console.WriteLine();
            Console.WriteLine("Options available:");
            Console.WriteLine("    --silent            No console log messages.");
            Console.WriteLine("    --nosummaryedit     Don't leave a summary on the summary page.");
            Console.WriteLine("    --noedit            Don't edit at all.");
            Console.WriteLine("    --editlimit <n>     Only make <n> edits.");
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
                Console.WriteLine("Page: " + locationData.Key);
                    ////string.Format(format, locationData.Value.Latitude, locationData.Value.Longitude);
                try
                {
                    report += ":* [[" + locationData.Key + "]]: " + string.Format(coordInline, locationData.Value.Latitude, locationData.Value.Longitude) + "\n";

                    string text = "";
                    try
                    {
                        text = api.GetPageContent(locationData.Key);
                    }
                    catch (MediaWikiException ex)
                    {
                        report += "::Error retriving page contents: " + ex.Message + "\n:::Skipping.\n";
                        continue;
                    }

                    if (ApplyCoords(ref text, locationData.Value))
                    {
                        try
                        {
                            if (!doEdit(locationData.Key, text, "Adding coordinates from KML file ([[WP:BOT|BOT]])", MediaWikiApi.ACTION_EDIT_EXISTS_DOESEXIST, true, true, MediaWikiApi.ACTION_EDIT_TEXT_REPLACE, MediaWikiApi.ACTION_EDIT_SECTION_ALL))
                            {// one edit remaining, please report
                                report += "::Edit limit too low. Terminating bot run.";
                                break;
                            }
                        }
                        catch (MediaWikiException ex)
                        {
                            report += "::Error saving page: " + ex.Message + "\n:::Skipping.\n";
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    report += "::Error: " + ex.Message + "\n:::Skipping.\n";
                }
            }

            SendReport(report);
        }

        /// <summary>
        /// Sends a report to the operator of the bot
        /// </summary>
        /// <param name="report">the report to be sent</param>
        private static void SendReport(string report)
        {
            try
            {
                if (summaryEdit)
                {
                    api.Edit(reportPage, report + "\n--~~~~", "Report for " + DateTime.UtcNow.ToLongDateString() + " " + DateTime.UtcNow.ToShortTimeString(), MediaWikiApi.ACTION_EDIT_EXISTS_NOCHECK, false, true, MediaWikiApi.ACTION_EDIT_TEXT_REPLACE, MediaWikiApi.ACTION_EDIT_SECTION_NEW);
                    editCount++;
                }
            }
            catch (MediaWikiException ex)
            {
                report += "Error saving report to wiki: " + ex.Message + "\n";
            }

            System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage("stwalkercoordbot@helpmebot.org.uk", reportEmail);
            mail.Body = report;
            mail.Subject = "StwalkerCoordBot report";
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("helpmebot.org.uk");
            smtp.Send(mail);
        }

        private static bool ApplyCoords(ref string wikitext, Location coords)
        {
            string wtBackup = wikitext;

            if (Regex.IsMatch(wikitext, @"\{\{(nobots|bots\|(allow=none|deny=(?!none).*(" + username.Normalize() + @"|all)|optout=all))\}\}", RegexOptions.IgnoreCase))
            {
                report += "::Bot excluded from page.\n";
                return false;
            }

            if (Regex.IsMatch(wikitext, @"\{\{[Cc]oord\|"))
            {
                report += "::Already has coordinates template, skipping.\n";
                return false;
            }

            string pattern = @"^\|[ ]*coordinates[ ]*=[ ]*$";
            string replacement = "| coordinates = " + string.Format(coordInlineTitle, coords.Latitude, coords.Longitude);
            wikitext = Regex.Replace(wikitext, pattern, replacement, RegexOptions.Multiline);

            pattern = @"^\|[ ]*coords[ ]*=[ ]*$";
            replacement = "| coords = " + string.Format(coordInlineTitle, coords.Latitude, coords.Longitude);
            wikitext = Regex.Replace(wikitext, pattern, replacement, RegexOptions.Multiline);

            if (!wikitext.Equals(wtBackup))
            {
                report += "::Added coordinates to infobox.(display=inline,title)\n";
            }

            else
            {
                wikitext = wikitext + "\n\n" + string.Format(coordTitle, coords.Latitude, coords.Longitude);
                report += "::Added coordinates to end of article.(display=title)\n";
            }

            return true;
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
            while (xpni.MoveNext())
            {
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

            report += string.Format(":Found {0} coordinate pairs.\n", locs.Count);

            return locs;
        }

        private static bool doEdit(string title, string text, string summary, int exists, bool minor, bool bot, int location, int section)
        {
            if (editLimit == -1 || editLimit > ( summaryEdit ? editCount + 1 : editCount ))
            {
                if (doEdits)
                    api.Edit(title, text, summary, exists, minor, bot, location, section);
                else
                    report += "::Editing disabled. Edit not saved.";
                editCount++;
                return true;
            }
            else
            {
                return false;
            }
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