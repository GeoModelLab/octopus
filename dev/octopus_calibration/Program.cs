using System.Text.Json;
using Models.Datatype;
using octoPusAI.ModelCallers;
using octoPusAI.Readers;
using UNIMI.optimizer;


#region json settings
// if no argument is provided, default to HurrayConfig.json
string fileName = args.Length > 0 ? args[0] : "octoPus.json";

if (!File.Exists(fileName))
{
    Console.WriteLine($"Config file not found: {fileName}");
    return;
}

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
//type of weather data (either hourly or daily)
string WeatherTimeStep = config.settings.WeatherTimeStep.ToLower();
bool useLLM = (bool)config.settings.useLLM;
bool useRandomForest = (bool)config.settings.useRandomForest;
bool useConsole = (bool)config.settings.useConsole;
string referenceFile = config.paths.referenceFile;
string bbch_Reference = config.paths.bbchReference;
string calibrationVariable = config.settings.calibrationVariable;
List<string> modelsToRun = config.settings.modelsToRun;

Console.WriteLine("I am ready to start the simulation for the following sites: {0}.", string.Join(", ", sites));
Console.WriteLine("The simulation will run from {0} to {1}", startYear, endYear);
Console.WriteLine("The Llama assistant for decision support will be called when computed risk is higher than {0}.", assistantRisk);
Console.WriteLine("The number of models to trigger a very high risk is set to {0}.", veryHighModelsThreshold);
Console.WriteLine("");
Console.WriteLine("To change these settings, edit the octoPus.json configuration file.\nMore information on https://gitlab.com/octoPus README\n");

//Console.ReadLine();
Console.ForegroundColor = ConsoleColor.White;

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
DirectoryInfo directoryInfo = new DirectoryInfo(weatherDir );
FileInfo[] files = directoryInfo.GetFiles();
List<string> availableSites = files.Select(x => x.Name).ToList();
#endregion

//instantiate the Runner class and populate its properties
var _runner = new octoPusRunner();
_runner.modelPath = LLMfile;
_runner.Rversion = Rversion;

//read model names and parameters
var model_param_range = paramRangeReader(octoPusParametersFile);

List<string> toExclude = new List<string>() { "Phenology", "BBCH", "Incubation" };
if(calibrationVariable == "Phenology")
{
    toExclude = new List<string>() { "Incubation" };
}


#region read reference data

    //onset date
    ReferenceReader _refReader = new ReferenceReader();
    var refData = _refReader.readReference(referenceFile);

    // bbch 
    BBCHReferenceReader _refReaderbbch = new BBCHReferenceReader();
    var refDatabbch = _refReaderbbch.BbchreadReference(bbch_Reference);


#endregion

#region read calibrated phenology parameters
var site_phenoParam = paramReader.read_calibPhenoParam(new DirectoryInfo("calibratedParametersPhenology").
    GetFiles().ToList());


#endregion

#region read weather data
//read weather data
var weatherData = new Dictionary<string, Dictionary<DateTime, Input>>();
WeatherReader weatherReader = new WeatherReader();
foreach (var site in availableSites)
{

    weatherData.Add(site, new Dictionary<DateTime, Input>());
    string weatherFile = site;
    switch (WeatherTimeStep)
    {
        case "hourly":
            weatherData[site] = weatherReader.readHourly(weatherFile, startYear, endYear);
            break;

        case "daily":
            Dictionary<DateTime, InputDaily> weatherDataH = weatherReader.readDaily(weatherDir + "\\" + weatherFile, startYear, endYear);
            foreach (var day in weatherDataH.Keys)
            {
                weatherData[site].AddRange(weatherReader.estimateHourly(weatherDataH[day], day));
            }
            break;

        default:
            Console.WriteLine("Check the WeatherTimeStep in the octoPus.json file, available choices are: \"daily\" or \"hourly\"");
            break;
    }
}


#endregion


#region execute epidemiological models (the tentacles)

//loop over models
foreach (var model in model_param_range.Keys)
{
    if (modelsToRun.Contains(model))
    {
        //to avoid calibrating on parameter classes different from the eight 'octopus models'
        if (!toExclude.Contains(model))
        {
            #region define optimizer settings
            // Multistart Nelder–Mead simplex configuration:
            // - NofSimplexes: number of random starts
            // - Ftol: tolerance on objective function for convergence
            // - Itmax: maximum iterations per simplex
            var msx = new MultiStartSimplex();
            msx.NofSimplexes = 1;
            msx.Ftol = 0.000000000001;
            msx.Itmax = 1000;
            #endregion

            #region Define parameter settings for calibration

            
            // The entire parameter space (nameParam) is available to the optimizer
            Dictionary<string, ParameterRange> nameParam = model_param_range[model];
            if(calibrationVariable == "Phenology")
            {
                foreach(var name in model_param_range["BBCH"].Keys)
                {
                    nameParam.Add(name, model_param_range["BBCH"][name]);
                }
            }
            _runner.nameParam = nameParam;

            // Determine which parameters are in the calibration subset
            int paramCalibrated = 0;
            var param_outCalibration = new Dictionary<string, float>();
            var calibratedParamNames = new List<string>();

            // Decide: parameters matching the calibrationVariable (or "all") and marked with a non-empty calibration tag
            foreach (var kvp in nameParam)
            {
                string name = kvp.Key;
                var param = kvp.Value;

                if (!string.IsNullOrWhiteSpace(param.calibration))
                {
                    paramCalibrated++;
                    calibratedParamNames.Add(name);
                }
                else
                {
                    // Keep default value for parameters outside the calibration subset
                    param_outCalibration[name] = param.value;
                }
            }

            // Build bounds array (Limits) for the calibrated subset [min, max] per parameter
            double[,] Limits = new double[paramCalibrated, 2];
            for (int i = 0; i < calibratedParamNames.Count; i++)
            {
                var name = calibratedParamNames[i];
                var param = nameParam[name];
                Limits[i, 0] = param.min;
                Limits[i, 1] = param.max;
            }


            #endregion

            //message to console
            Console.WriteLine("CALIBRATION STARTED FOR MODEL {0}", model);

            //set runner properties
            _runner.availableSites = availableSites;
            _runner.modelPath = LLMfile;
            _runner.Rversion = Rversion;
            _runner.WeatherTimeStep = WeatherTimeStep;
            _runner.BBCH_Susceptibility = BBCH_susceptibility;
            //_runner.weatherFile = weatherDir + "\\" + WeatherTimeStep + "\\" + site; //edit euge
            _runner.octoPusParameters = octoPusParameters;
            _runner.startYear = startYear;
            _runner.endYear = endYear;
            _runner.assistantRisk = assistantRisk;
            _runner.veryHighModelsThreshold = veryHighModelsThreshold;
            _runner.useLLM = useLLM;
            _runner.useRandomForest = useRandomForest;
            _runner.useConsole = useConsole;
            _runner.modelUnderOptimization = model;
            _runner.weatherDir = weatherDir;
            _runner.param_outCalibration = param_outCalibration;
            _runner.areEPIDMCASTexecutable = true;
            _runner.site_year_onsetDate = refData;
            _runner.site_phenoParam_value = site_phenoParam;
            _runner.weatherData = weatherData;
            
            if (calibrationVariable == "Onset")
            {
                #region run octoPus
                //empty list of dates and SWELL outputs
                var dateOutputs = new Dictionary<DateTime, OutputsDaily>();
                // Run the multistart simplex optimizer
                // Results buffer returned by the optimizer (1 row x N params here)
                double[,] results = new double[1, 1];
                msx.Multistart(_runner, paramCalibrated, Limits, out results);

                //get calibrated parameters
                var paramCalibValue = new Dictionary<string, float>();
                int count = 0;

                #region write calibrated parameters
                string header = "param, value";
                List<string> writeParam = new List<string>();
                writeParam.Add(header);
                foreach (var param in calibratedParamNames)
                {
                    //write a line for each parameter
                    string line = "";
                    line += param + ",";
                    line += results[0, count];
                    writeParam.Add(line);
                    paramCalibValue.Add(param, (float)results[0, count]);
                    count++;
                }

                //write calibrated parameters to file
                System.IO.File.WriteAllLines("calibratedParameters//calibParam_" + model + ".csv", writeParam);
                #endregion

                //execute model with calibrated parameters
                _runner.oneShot(paramCalibValue);
                #endregion
            }
            else if (calibrationVariable == "Phenology")
            {
                foreach(var site in availableSites)
                {
                    var siteKey = site.Substring(0, site.Length - 4);

                    if (refDatabbch.ContainsKey(siteKey))
                    {



                        //message to console
                        Console.WriteLine("CALIBRATION STARTED FOR SITE {0}", site);

                        var singleSite = new List<string>();
                        singleSite.Add(site);
                        _runner.availableSites = singleSite;

                        _runner.calibrationVariable = calibrationVariable;

                        _runner.Year_bbchDate_Ref = refDatabbch[siteKey];


                        #region run phenology model
                        //empty list of dates and SWELL outputs
                        var dateOutputs = new Dictionary<DateTime, OutputsDaily>();
                        // Run the multistart simplex optimizer
                        // Results buffer returned by the optimizer (1 row x N params here)
                        double[,] results = new double[1, 1];
                        msx.Multistart(_runner, paramCalibrated, Limits, out results);

                        //get calibrated parameters
                        var paramCalibValue = new Dictionary<string, float>();
                        int count = 0;

                        #region write calibrated parameters
                        string header = "param, value";
                        List<string> writeParam = new List<string>();
                        writeParam.Add(header);
                        foreach (var param in calibratedParamNames)
                        {
                            //write a line for each parameter
                            string line = "";
                            line += param + ",";
                            line += results[0, count];
                            writeParam.Add(line);
                            paramCalibValue.Add(param, (float)results[0, count]);
                            count++;
                        }

                        //write calibrated parameters to file
                        System.IO.File.WriteAllLines("calibratedParametersPhenology//calibParam_" + site, writeParam);
                        #endregion

                        //execute model with calibrated parameters
                        _runner.oneShot(paramCalibValue);

                        #endregion
                    }
                }
            }
        }
    }
}

#endregion

//read model and parameters from file
Dictionary<string, Dictionary<string, ParameterRange>> paramRangeReader(string fileName)
{
    var mod_par_ran = new Dictionary<string, Dictionary<string, ParameterRange>>();
    //open the stream
    StreamReader sr = new StreamReader(fileName);
    sr.ReadLine(); //header

    while (!sr.EndOfStream)
    {
        string[] line = sr.ReadLine().Split(',');

        string model = line[0];
        string parameter = line[1];
        float min = float.Parse(line[2]);
        float max = float.Parse(line[3]);
        float value = float.Parse(line[4]);
        string calib = line[6];

        if (!mod_par_ran.ContainsKey(line[0]))
        {
            mod_par_ran.Add(model, new Dictionary<string, ParameterRange>());
        }
        ParameterRange parRange = new ParameterRange();
        parRange.max = max;
        parRange.min = min;
        parRange.value = value;
        parRange.calibration = calib;
        mod_par_ran[model].Add(parameter, parRange);

    }
    sr.Close();
    return(mod_par_ran);

}

public class ParameterRange
{
    public float min { get; set; }  
    public float max { get; set; }
    public float value { get; set; }
    public string calibration {  get; set; }
}




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
    public string? WeatherTimeStep { get; set; }

    public bool? useLLM { get; set; }
    public bool? useRandomForest { get; set; }
    public bool? useConsole { get; set; }
    public string? calibrationVariable { get; set; }
    public List<string>? modelsToRun { get; set; }

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

    public string? referenceFile { get; set; }
    public string? bbchReference { get; set; }
}
#endregion


