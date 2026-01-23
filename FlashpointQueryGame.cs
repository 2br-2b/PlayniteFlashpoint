using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flashpoint
{
    public class FlashpointGame
    {
        // These names must match the columns in your SQLite "game" table
        public string id { get; set; }
        public string title { get; set; }
        public string version { get; set; }
        public string originalDescription { get; set; }
        public string lastPlayed { get; set; } // 2026-01-22T12:04:10.702Z
        public string playtime { get; set; } // in seconds
        public string playCounter { get; set; }
        public string logoPath { get; set; }
        public string screenshotPath { get; set; }
    }
}