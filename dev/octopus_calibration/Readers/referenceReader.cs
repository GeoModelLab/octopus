using System.Collections.Generic;
using System.Globalization;
using Models.Datatype;

namespace octoPusAI.Readers
{
    //This class reads the References file
    // reference_file: CSV containing observed onset dates for model validation
    public class ReferenceReader
    {
        public Dictionary<string,Dictionary<string, Dictionary<int, DateTime>>> readReference(string file)
        {
            var refData = new Dictionary<string, Dictionary<string, Dictionary<int, DateTime>>>();


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
                    string site = line[1].Trim();
                    string onsetDateStr = line[2].Trim();
                    string yearStr = line[3].Trim();
                    string cluster = line[0].Trim();

                    // Parse onset date (format: M/d/yyyy)
                    DateTime onsetDate;
                    DateTime.TryParseExact(onsetDateStr, "M/d/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out onsetDate);

                    // Parse year
                    int year = int.TryParse(yearStr, out int y) ? y : 0;

                    if(!refData.ContainsKey(cluster))
                    {
                        refData.Add(cluster, new Dictionary<string, Dictionary<int, DateTime>>());
                    }

                    if (!refData[cluster].ContainsKey(site))
                    {
                        refData[cluster].Add(site, new Dictionary<int, DateTime>());
                    }
                    refData[cluster][site].Add(year, onsetDate);

                }
                sr.Close();
            }
            return refData;
        }

        // Limite inferiore della finestra di onset (L, 5a colonna "lowerDate").
        // Colonna assente o vuota -> L = U (errore classico).
        public Dictionary<string, Dictionary<string, Dictionary<int, DateTime>>> readLowerReference(string file)
        {
            var lowData = new Dictionary<string, Dictionary<string, Dictionary<int, DateTime>>>();
            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    string[] line = sr.ReadLine().Split(',');
                    string cluster = line[0].Trim();
                    string site = line[1].Trim();
                    int year = int.TryParse(line[3].Trim(), out int y) ? y : 0;

                    DateTime onsetDate;
                    DateTime.TryParseExact(line[2].Trim(), "M/d/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out onsetDate);

                    DateTime lowerDate = onsetDate; // default L = U
                    if (line.Length > 4 && !string.IsNullOrWhiteSpace(line[4]) &&
                        DateTime.TryParseExact(line[4].Trim(), "M/d/yyyy", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTime l))
                    {
                        lowerDate = l;
                    }

                    if (!lowData.ContainsKey(cluster))
                        lowData.Add(cluster, new Dictionary<string, Dictionary<int, DateTime>>());
                    if (!lowData[cluster].ContainsKey(site))
                        lowData[cluster].Add(site, new Dictionary<int, DateTime>());
                    lowData[cluster][site].Add(year, lowerDate);
                }
            }
            return lowData;
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
                    DateTime.TryParseExact(bbchDateStr, "M/d/yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out bbchDate);

                    // Parse year
                    int year = int.TryParse(yearStr, out int y) ? y : 0;
                    // Parse bbch
                    int bbch = int.TryParse(bbchStr, out int z) ? z : 0;

                    //TODO: only for phenology calibration
                    if (!refDatabbch.ContainsKey(site) && bbch>=8 && bbch <=15)
                    { 
                        refDatabbch.Add(site, new Dictionary<int, Dictionary<int, DateTime>>());
                    }

                    if (refDatabbch.ContainsKey(site))
                    {

                        if (!refDatabbch[site].ContainsKey(year))
                        {
                            refDatabbch[site].Add(year, new Dictionary<int, DateTime>());
                        }

                        refDatabbch[site][year].Add(bbch, bbchDate);
                    }
                }
                sr.Close();
            }
            return refDatabbch;
        }
    }
}



