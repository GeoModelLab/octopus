using Models.Datatype;

namespace octoPusAI.Readers
{
    //This class reads the parameters from the file
    public class ParametersReader
    {
        //The Dictionary has the class name and the parameter name as key and the parameter value as value
        public Dictionary<string, float> read(string file)
        {
            var nameParam = new Dictionary<string, float>();

            //read parameters
            StreamReader sr = new StreamReader(file);
            sr.ReadLine();
            //loop over the file
            while (!sr.EndOfStream)
            {
                string[] line = sr.ReadLine().Split(',');
                nameParam.Add(line[0] + "_" + line[1], float.Parse(line[4]));
            }
            //close the file
            sr.Close();

            return nameParam;
        }

        //The Dictionary has the BBCH code as key and the susceptibility as value
        public Dictionary<int, parametersSusceptibility> BBCH_Susceptibility(string ParameterFile)
        {
            Dictionary<int, parametersSusceptibility> _bBCH_Susceptibility = new Dictionary<int, parametersSusceptibility>();

            //Read parameter file
            StreamReader sr = new StreamReader(ParameterFile);
            sr.ReadLine();

            while (!sr.EndOfStream)
            {
                string[] line = sr.ReadLine().Split(',');
                parametersSusceptibility parametersSusceptibility = new parametersSusceptibility();
                parametersSusceptibility.susceptibility = float.Parse(line[1]);
                _bBCH_Susceptibility.Add(Convert.ToInt32(line[0]), parametersSusceptibility);
            }
            //close the file
            sr.Close();

            return _bBCH_Susceptibility;

        }
    }
}
