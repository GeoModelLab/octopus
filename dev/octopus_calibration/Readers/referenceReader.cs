using System.Collections.Generic;
using System.Globalization;
using Models.Datatype;

namespace octoPusAI.Readers
{
    //This class reads the References file
    // reference_file: CSV containing observed onset dates for model validation
    public class ReferenceReader
    {
        public Dictionary<string, Dictionary<int, DateTime>> readReference(string file)
        {
            var refData = new Dictionary<string, Dictionary<int, DateTime>>();

            //read the file
            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                //skip the first line
                sr.ReadLine();

                while (!sr.EndOfStream)
                {
                    // Split the line by comma (adjust the split according to your system)
                    string[] line = sr.ReadLine().Split(',');

                    // Read the three columns
                    string site = line[0].Trim();
                    string onsetDateStr = line[1].Trim();
                    string yearStr = line[2].Trim();

                    // Parse onset date (format: M/d/yyyy)
                    DateTime onsetDate;
                    DateTime.TryParseExact(onsetDateStr, "M/d/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out onsetDate);

                    // Parse year
                    int year = int.TryParse(yearStr, out int y) ? y : 0;

                    if (!refData.ContainsKey(site))
                    {
                        refData.Add(site, new Dictionary<int, DateTime>());
                    }
                    refData[site].Add(year, onsetDate);
                }
                sr.Close();
            }
            return refData;
        }
    }
    public class BBCHReferenceReader
    {
        public Dictionary<string, Dictionary<int, Dictionary<int, DateTime>>> BbchreadReference(string file)
        {
            var refDatabbch = new Dictionary<string, Dictionary<int, Dictionary<int, DateTime>>>();

            //read the file
            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                //skip the first line
                sr.ReadLine();

                while (!sr.EndOfStream)
                {
                    // Split the line by comma (adjust the split according to your system)
                    string[] line = sr.ReadLine().Split(',');

                    // Read the three columns
                    string site = line[0].Trim('"');
                    string province = line[1].Trim('"');
                    string yearStr = line[2].Trim('"');
                    string bbchStr = line[3].Trim('"');
                    string bbchDateStr = line[4].Trim('"');

                    // Parse bbch date (format: M/d/yyyy)
                    DateTime bbchDate;
                    DateTime.TryParseExact(bbchDateStr, "M/d/yy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out bbchDate);

                    // Parse year
                    int year = int.TryParse(yearStr, out int y) ? y : 0;
                    // Parse bbch
                    int bbch = int.TryParse(bbchStr, out int z) ? z : 0;

                    if (!refDatabbch.ContainsKey(site))
                    { 
                        refDatabbch.Add(site, new Dictionary<int, Dictionary<int, DateTime>>());
                    }
                   
                    if (!refDatabbch[site].ContainsKey(year))
                    {      
                        refDatabbch[site].Add(year, new Dictionary<int, DateTime>());
                    }

                    refDatabbch[site][year].Add(bbch, bbchDate);
                }
                sr.Close();
            }
            return refDatabbch;
        }
    }
}



