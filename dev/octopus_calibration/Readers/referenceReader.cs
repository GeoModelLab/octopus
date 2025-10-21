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
}



