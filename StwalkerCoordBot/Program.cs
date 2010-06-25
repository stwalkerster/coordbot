// <copyright file="Program.cs" company="Simon Walker">
// This work is licensed under the Creative Commons Attribution-Share Alike 3.0 Unported License. To view a copy of this license, visit http://creativecommons.org/licenses/by-sa/3.0/ or send a letter to Creative Commons, 171 Second Street, Suite 300, San Francisco, California, 94105, USA.
// </copyright>

namespace StwalkerCoordBot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
        private const string COORD_INLINE_TITLE = "{{{{coord|{0}|N|{1}|E|display=inline,title}}}}";
        private const string COORD_INLINE = "{{{{coord|{0}|N|{1}|E|display=inline}}}}";
        private const string COORD_TITLE = "{{{{coord|{0}|N|{1}|E|display=title}}}}";

        /// <summary>
        /// Email address to send the report to.
        /// </summary>
        private const string REPORT_EMAIL = "stwalkerster@helpmebot.org.uk";
        private const string REPORT_PAGE = "User:Stwalkerbot/Coordinates report";
        private static string _report = "";

        private static bool _silent;
        private static bool _summaryEdit = true;
        private static bool _doEdits = true;
        private static int _editCount;


        private static string _username;

        private static MediaWikiApi _api;

        /// <summary>
        /// Main method, initialises the bot
        /// </summary>
        /// <param name="args">Program arguments passed to the executable</param>
// ReSharper disable InconsistentNaming
        public static void Main(string[] args)
// ReSharper restore InconsistentNaming
        {
            if (args.Length < 1)
            {
                printHelp();
                return;
            }

            _api = new MediaWikiApi();

            setOptions(args);

            Console.Write("Bot username: ");
            _username = Console.ReadLine();
            Console.Write("Bot password: ");
            string password = Console.ReadLine();

            _api.login(_username, password);

            FileInfo fi = new FileInfo(args[0]);

            if (fi.Extension != ".kml") return;

            _report += ":Using KML file: " + args[0] + "\n";

            runBot(getLocations(args[0]));
        }

        private static void setOptions(IEnumerable<string> args)
        {
            if (args.Contains("--silent"))
            {
                _silent = true;
            }

            if (args.Contains("--nosummaryedit"))
                _summaryEdit = false;

            if (args.Contains("--noedit"))
                _doEdits = false;
        }

        /// <summary>
        /// Prints out useage help if invalid params are given
        /// </summary>
        private static void printHelp()
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
        private static void runBot(Dictionary<string, Location> locations)
        {
            if (locations == null)
                return;


            foreach (KeyValuePair<string, Location> locationData in locations)
            {
                if(!_silent) Console.WriteLine("Page: " + locationData.Key);
                    ////string.Format(format, locationData.Value.Latitude, locationData.Value.Longitude);
                try
                {
                    _report += ":* [[" + locationData.Key + "]]: " + string.Format(COORD_INLINE, locationData.Value.latitude, locationData.Value.longitude) + "\n";

                    string text;
                    try
                    {
                        text = _api.getPageContent(locationData.Key);
                    }
                    catch (MediaWikiException ex)
                    {
                        _report += "::Error retriving page contents: " + ex.Message + "\n:::Skipping.\n";
                        continue;
                    }

                    if (applyCoords(ref text, locationData.Value))
                    {
                        try
                        {
                            if (!doEdit(locationData.Key, text, "Adding coordinates from KML file ([[WP:BOT|BOT]])", MediaWikiApi.ACTION_EDIT_EXISTS_DOESEXIST, true, true, MediaWikiApi.ACTION_EDIT_TEXT_REPLACE, MediaWikiApi.ACTION_EDIT_SECTION_ALL))
                            {// one edit remaining, please report
                                _report += "::Edit limit too low. Terminating bot run.";
                                break;
                            }
                        }
                        catch (MediaWikiException ex)
                        {
                            _report += "::Error saving page: " + ex.Message + "\n:::Skipping.\n";
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _report += "::Error: " + ex.Message + "\n:::Skipping.\n";
                }
            }

            sendReport(_report);
        }

        /// <summary>
        /// Sends a report to the operator of the bot
        /// </summary>
        /// <param name="report">the report to be sent</param>
        private static void sendReport(string report)
        {
            try
            {
                if (_summaryEdit)
                {
                    _api.edit(REPORT_PAGE, report + "\n--~~~~", "Report for " + DateTime.UtcNow.ToLongDateString() + " " + DateTime.UtcNow.ToShortTimeString(), MediaWikiApi.ACTION_EDIT_EXISTS_NOCHECK, false, true, MediaWikiApi.ACTION_EDIT_TEXT_REPLACE, MediaWikiApi.ACTION_EDIT_SECTION_NEW);
                    _editCount++;
                }
            }
            catch (MediaWikiException ex)
            {
                report += "Error saving report to wiki: " + ex.Message + "\n";
            }

            System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage("stwalkercoordbot@helpmebot.org.uk", REPORT_EMAIL)
                                                   {
                                                       Body = report,
                                                       Subject = "StwalkerCoordBot report"
                                                   };
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient("helpmebot.org.uk");
            smtp.Send(mail);
        }

        private static bool applyCoords(ref string wikitext, Location coords)
        {
            string wtBackup = wikitext;

            if (Regex.IsMatch(wikitext, @"\{\{(nobots|bots\|(allow=none|deny=(?!none).*(" + _username.Normalize() + @"|all)|optout=all))\}\}", RegexOptions.IgnoreCase))
            {
                _report += "::Bot excluded from page.\n";
                return false;
            }

            if (Regex.IsMatch(wikitext, @"\{\{[Cc]oord\|"))
            {
                _report += "::Already has coordinates template, skipping.\n";
                return false;
            }

            string pattern = @"^\|[ ]*coordinates[ ]*=[ ]*$";
            string replacement = "| coordinates = " + string.Format(COORD_INLINE_TITLE, coords.latitude, coords.longitude);
            wikitext = Regex.Replace(wikitext, pattern, replacement, RegexOptions.Multiline);

            pattern = @"^\|[ ]*coords[ ]*=[ ]*$";
            replacement = "| coords = " + string.Format(COORD_INLINE_TITLE, coords.latitude, coords.longitude);
            wikitext = Regex.Replace(wikitext, pattern, replacement, RegexOptions.Multiline);

            if (!wikitext.Equals(wtBackup))
            {
                _report += "::Added coordinates to infobox.(display=inline,title)\n";
            }

            else
            {
                wikitext = wikitext + "\n\n" + string.Format(COORD_TITLE, coords.latitude, coords.longitude);
                _report += "::Added coordinates to end of article.(display=title)\n";
            }

            return true;
        }

        /// <summary>
        /// Parses the kml file
        /// </summary>
        /// <param name="filePath">Path of the KML file</param>
        /// <returns>a dictionary of required edits</returns>
        private static Dictionary<string, Location> getLocations(string filePath)
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

            _report += string.Format(":Found {0} coordinate pairs.\n", locs.Count);

            return locs;
        }

        private static bool doEdit(string title, string text, string summary, int exists, bool minor, bool bot, int location, int section)
        {
                if (_doEdits)
                    _api.edit(title, text, summary, exists, minor, bot, location, section);
                else
                    _report += "::Editing disabled. Edit not saved.";
                _editCount++;
                return true;
        }


        /// <summary>
        /// A location structure which holds the Lat/Long information
        /// </summary>
        private struct Location
        {
            /// <summary>
            /// The latitude of the location. Internal field, inaccessible
            /// </summary>
            private readonly float _latitude;

            /// <summary>
            /// The longitude of the location. Internal field, inaccessible
            /// </summary>
            private readonly float _longitude;

            /// <summary>
            /// Initializes a new instance of the Location struct.
            /// </summary>
            /// <param name="latitude">Latitude of the location</param>
            /// <param name="longitude">Longitude of the location</param>
            public Location(float latitude, float longitude)
            {
                _latitude = latitude;
                _longitude = longitude;
            }

            /// <summary>
            /// Gets the latitude of the location
            /// </summary>
            public float latitude
            {
                get
                {
                    return _latitude;
                }
            }

            /// <summary>
            /// Gets the longitude of the location
            /// </summary>
            public float longitude
            {
                get
                {
                    return _longitude;
                }
            }
        }
    }
}