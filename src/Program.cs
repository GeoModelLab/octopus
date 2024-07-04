using System.Runtime.CompilerServices;
using System.Text.Json;
using LLama.Native;
using Models.Datatype;
using octoPusAI.ModelCallers;
using octoPusAI.Readers;

//config Llama
NativeLibraryConfig.Instance.WithLogCallback(delegate (LLamaLogLevel level, string message) { Console.Write($"{level}: {message}"); });

#region Console welcome message
Console.Title = "octoPus";
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("                                                      _        ____");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("                                            ___   ___| |_ ___ |  _ \\ _   _ ___ ");
Console.ForegroundColor = ConsoleColor.DarkCyan;
Console.WriteLine("                                           / _ \\ / __| __/ _ \\| |_) | | | / __|   ");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("   \\(^^)/ °  ~ |_(°°)_| ~  ° _(--)_ °  ~  | (_) | (__| | |(_) |  __/| |_| \\__ \\ ~  ° ~(^^)~ °  ~ _(°°)_ °  ~  ~(**)~");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("   ((())) ~  °   (())   °  ~ |||||| ~  °   \\___/ \\___|\\__\\___/|_|    \\__,_|___/ °  ~ ((())) ~  ° //()\\\\ ~  °  ((()))  ");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("");

Console.WriteLine("RISK LEGEND");
Console.ForegroundColor = ConsoleColor.Blue;
Console.WriteLine(" very low  |");
Console.WriteLine("  \\(^^)/   |    HELLO! I am octoPus your decision support system for grapevine primary downy mildew (Plasmopara viticola). ");
Console.WriteLine("  ((()))   |");
Console.WriteLine("           |");
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("   low     |");
Console.WriteLine("  ~(^^)~   |    My eyes are models to simulate grapevine phenology and host susceptibility to the pathogen.");
Console.WriteLine("  ((()))   |");
Console.WriteLine("           |");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("  medium   |");
Console.WriteLine(" |_(°°)_|  |    My eight tentacles are disease models sensing the weather suitability to pathogens infection.");
Console.WriteLine("   (())    |");
Console.WriteLine("           |");
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("   high    |");
Console.WriteLine("  _(°°)_   |    My brain is a machine learning model elaborating these information to predict the daily infection risk.");
Console.WriteLine("  //()\\\\   |");
Console.WriteLine("           |");
Console.ForegroundColor = ConsoleColor.DarkRed;
Console.WriteLine(" very high |");
Console.WriteLine("  _(--)_   |    My mouth is a large language model elaborating a decision support message for farmers.");
Console.WriteLine("  ||||||   |\r\n");

Console.ForegroundColor = ConsoleColor.White;


#endregion

#region json settings
//read json configuration file
string fileName = "octoPus.json";
string jsonString = File.ReadAllText(fileName);
var config = JsonSerializer.Deserialize<root>(jsonString);

#region assign json parameters to local variables
//start and end year
int startYear = config.settings.startYear.GetValueOrDefault();
int endYear = config.settings.endYear.GetValueOrDefault();
//sites
var sites = config.settings.sites;
//risk level to call the Llama assistant - values: very low, low, medium, high, very high
float assistantRisk = config.settings.assistantRisk.GetValueOrDefault(); 
var veryHighModelsThreshold = config.settings.veryHighModelsThreshold.GetValueOrDefault();
//parameters files
string octoPusParametersFile = config.paths.octoPusParametersPath;
string hostSusceptibilityFile = config.paths.susceptibilityFileBBCH;
string weatherDir = config.paths.weatherDir;
string LLMfile = config.paths.LLMfile;
string Rversion = config.paths.Rversion;

Console.WriteLine("I am ready to start the simulation for the following sites: {0}.", string.Join(", ", sites));
Console.WriteLine("The simulation will run from {0} to {1}", startYear, endYear);
Console.WriteLine("The Llama assistant for decision support will be called when computed risk is higher than {0}.", assistantRisk);
Console.WriteLine("The number of models to trigger a very high risk is set to {0}.", veryHighModelsThreshold);
Console.WriteLine("");
Console.WriteLine("To change these settings, edit the octoPus.json configuration file.\nMore information on https://gitlab.com/octoPus README\n");

Console.ReadLine();
Console.ForegroundColor = ConsoleColor.DarkGray;

#endregion

#endregion

#region parameters files
//instantiate the ParamReader class
var paramReader = new ParametersReader();
//read octoPus parameters file
var octoPusParameters = paramReader.read(octoPusParametersFile);
//read BBCH susceptibility file
var BBCH_susceptibility = paramReader.BBCH_Susceptibility(hostSusceptibilityFile);
#endregion

#region weather data files
DirectoryInfo directoryInfo = new DirectoryInfo(weatherDir);
FileInfo[] files = directoryInfo.GetFiles();
List<string> availableSites = files.Select(x => x.Name).ToList();
#endregion

//instantiate the Runner class and populate its properties
var _runner = new octoPusRunner();
_runner.modelPath = LLMfile;
_runner.Rversion = Rversion;

#region execute epidemiological models (the tentacles)
foreach (var site in availableSites)
{
    //execute only for the sites in the json configuration file
    if (!sites.Contains(site))
    {  
        continue;
    }
    else
    {
        //message to console
        Console.WriteLine("");
        Console.WriteLine("");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("SIMULATION STARTED ON FILE {0}", site);

        //set runner properties
        _runner.BBCH_Susceptibility = BBCH_susceptibility;
        _runner.weatherFile = weatherDir + "\\" + site;
        //check the availability of at least 12 months for executing EPI and DM-CAST
        checkWeatherAvailability checkWeatherAvailability   = new checkWeatherAvailability();
        float numberOfYear = 0;
        

        _runner.octoPusParameters = octoPusParameters;
        _runner.startYear = startYear;
        _runner.endYear = endYear;
        _runner.assistantRisk = assistantRisk;
        _runner.veryHighModelsThreshold = veryHighModelsThreshold;
        _runner.areEPIDMCASTexecutable = checkWeatherAvailability.areEPIDMCASTexecutable(_runner.weatherFile, out numberOfYear);

        #region manage EPI and DMcast execution with low number of weather data (at least 10 years should be available!)
        if (numberOfYear <1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("The weather file has less than one year of data!!!!");
            Console.WriteLine("The EPI and DMCAST models cannot be executed");
        }
        else if (numberOfYear < 10)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("The weather file has {0} years", numberOfYear);
            Console.WriteLine("The EPI and DMCAST models will be executed even if less than 10 years are available.");
        }
        Console.ForegroundColor = ConsoleColor.White;
        #endregion



        #region run octoPus
        //empty list of dates and SWELL outputs
        var dateOutputs = new Dictionary<DateTime, OutputsDaily>();
        //run octoPus
        _runner.octoPus(out dateOutputs);
        #endregion


    }
}


#endregion


#region json interfacing classes 

//contains the root of the json configuration file
public class root
{
    public settings? settings { get; set; }
    public paths? paths { get; set; }

}

//contains the settings in the json configuration file
public class settings
{
    public int? startYear { get; set; }
    public int? endYear { get; set; }
    public List<string>? sites { get; set; }
    public float? assistantRisk { get; set; }
    public int? veryHighModelsThreshold { get; set; }
}

//contains the paths in the json configuration file
public class paths
{
    public string? weatherDir { get; set; }
    public string? octoPusParametersPath { get; set; }
    public string? outputDir { get; set; }
    public string? susceptibilityFileBBCH { get; set; }
    public string? LLMfile { get; set; }
    public string? Rversion { get; set; }
}

#endregion

#region check weather data availability

public class checkWeatherAvailability
{
    public bool areEPIDMCASTexecutable(string weatherFile, out float numberOfYear)
    {

        bool areEPIDMCASTexecutable = false;

        //open the stream
        StreamReader sr = new StreamReader(weatherFile);
        //read the header
        sr.ReadLine();

        //create an index variable
        int line = 0;
        //first and last date
        DateTime firstDate = new DateTime();
        DateTime lastDate = new DateTime();

        while(!sr.EndOfStream)
        {
            string[] lineString = sr.ReadLine().Split(',');

            //date elements
            int year = int.Parse(lineString[1]);
            int month = int.Parse(lineString[2]);
            int day = int.Parse(lineString[3]);
            int hour = int.Parse(lineString[4]);

            //first line
            if (line == 0)
            {
                //set the date
                firstDate = new DateTime(year, month, day).AddHours(hour - 1);
            }
            lastDate = new DateTime(year, month, day).AddHours(hour - 1);
            line++;
        }
        sr.Close();

        TimeSpan timeSpan = lastDate-firstDate;

        numberOfYear = timeSpan.Days / 365;

        if(numberOfYear<1)
        {
            areEPIDMCASTexecutable = false;
        }
        else
        {
            areEPIDMCASTexecutable = true;
        }
        return areEPIDMCASTexecutable;

    }
}

#endregion