using Models.Datatype;

namespace octoPusAI.Readers
{
    //This class reads the weather from the file
    internal class ReferenceReader
    {
        public Dictionary<string, ReferenceData> readReference(string file)
        {
            var refData = new Dictionary<string, ReferenceData>();

            //read the file
            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                //skip the first line
                sr.ReadLine();

                //loop over the file
                while (!sr.EndOfStream)
                {
                    ////split the line by comma (adjust the split according to the settings of your laptop)
                    //string[] line = sr.ReadLine().Split(',');

                    ////create a new Input object
                    //ReferenceData ref = new ReferenceData();

                    ////date elements
                    //int year = int.Parse(line[1]);
                    //int month = int.Parse(line[2]);
                    //int day = int.Parse(line[3]);
                    //int hour = int.Parse(line[4]);
                    ////set the date
                    //gw.Date = new DateTime(year, month, day).AddHours(hour - 1);
                }
                //close the stream
                sr.Close();


            }

            //return the dictionary
            return refData;

        }
    }
}
