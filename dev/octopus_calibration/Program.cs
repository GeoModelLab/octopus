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
DirectoryInfo directoryInfo = new DirectoryInfo(weatherDir);
FileInfo[] files = directoryInfo.GetFiles();
List<string> availableSites = files.Select(x => x.Name).ToList();
#endregion

//instantiate the Runner class and populate its properties
var _runner = new octoPusRunner();
_runner.modelPath = LLMfile;
_runner.Rversion = Rversion;

//read model names and parameters
var model_param_range = paramRangeReader(octoPusParametersFile);

List<string> toExclude = new List<string>() { "Phenology", "BBCH" };
if (calibrationVariable == "Phenology")
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




// -------------------------------------------------------------------------
// FIX 1: separate phenology calibration branch from the onset/epi loop
// -------------------------------------------------------------------------
if (calibrationVariable == "Phenology")
{
    #region PHENOLOGY CALIBRATION

    // Build phenology parameter space once (Phenology + BBCH), using fresh copies
    // FIX 2: use new Dictionary copies to avoid mutating model_param_range between sites
    var nameParamPheno = new Dictionary<string, ParameterRange>();
    foreach (var kvp in model_param_range["Phenology"])
        nameParamPheno[kvp.Key] = kvp.Value;
    foreach (var kvp in model_param_range["BBCH"])
        nameParamPheno[kvp.Key] = kvp.Value;

    // Determine calibrated vs fixed parameters
    int paramCalibratedPheno = 0;
    var param_outCalibrationPheno = new Dictionary<string, float>();
    var calibratedParamNamesPheno = new List<string>();

    foreach (var kvp in nameParamPheno)
    {
        if (!string.IsNullOrWhiteSpace(kvp.Value.calibration))
        {
            paramCalibratedPheno++;
            calibratedParamNamesPheno.Add(kvp.Key);
        }
        else
        {
            param_outCalibrationPheno[kvp.Key] = kvp.Value.value;
        }
    }

    // Build bounds array
    double[,] LimitsPheno = new double[paramCalibratedPheno + 1, 2];
    for (int i = 0; i < calibratedParamNamesPheno.Count; i++)
    {
        LimitsPheno[i, 0] = nameParamPheno[calibratedParamNamesPheno[i]].min;
        LimitsPheno[i, 1] = nameParamPheno[calibratedParamNamesPheno[i]].max;
    }

    // FIX 3: loop directly over weather files (availableSites), filter by refDatabbch
    // files are named "weather_station_AL001.csv" → siteKey = "AL001"
    foreach (var siteFile in availableSites)
    {
        // strip "weather_station_" prefix and ".csv" suffix to get the site key
        string siteKey;
        string prefix = "weather_station_";
        if (siteFile.StartsWith(prefix))
            siteKey = siteFile.Substring(prefix.Length, siteFile.Length - prefix.Length - 4);
        else
            siteKey = siteFile.Substring(0, siteFile.Length - 4);

        if (!refDatabbch.ContainsKey(siteKey))
            continue;

        Console.WriteLine("CALIBRATION STARTED FOR SITE {0}", siteKey);

        // Read weather data for this site only — readHourly needs the full path
        string weatherFilePheno = Path.Combine(weatherDir, siteFile);
        var weatherDataPheno = new Dictionary<string, Dictionary<DateTime, Input>>();
        WeatherReader weatherReaderPheno = new WeatherReader();
        weatherDataPheno[siteKey] = new Dictionary<DateTime, Input>();
        switch (WeatherTimeStep)
        {
            case "hourly":
                weatherDataPheno[siteKey] = weatherReaderPheno.readHourly(weatherFilePheno, startYear, endYear);
                break;

            case "daily":
                Dictionary<DateTime, InputDaily> weatherDataHPheno = weatherReaderPheno.readDaily(weatherFilePheno, startYear, endYear);
                foreach (var day in weatherDataHPheno.Keys)
                {
                    weatherDataPheno[siteKey].AddRange(weatherReaderPheno.estimateHourly(weatherDataHPheno[day], day));
                }
                break;

            default:
                Console.WriteLine("Check the WeatherTimeStep in the octoPus.json file, available choices are: \"daily\" or \"hourly\"");
                break;
        }

        #region define optimizer settings
        var msxPheno = new MultiStartSimplex();
        msxPheno.NofSimplexes = 10;
        msxPheno.Ftol = 0.001;
        msxPheno.Itmax = 10000;
        #endregion

        // set runner properties
        _runner.nameParam = nameParamPheno;
        _runner.param_outCalibration = param_outCalibrationPheno;
        _runner.calibrationVariable = calibrationVariable;
        _runner.modelUnderOptimization = "Phenology";
        _runner.availableSites = new List<string> { siteKey };
        _runner.weatherData = weatherDataPheno;
        _runner.Year_bbchDate_Ref = refDatabbch[siteKey];
        _runner.modelPath = LLMfile;
        _runner.Rversion = Rversion;
        _runner.WeatherTimeStep = WeatherTimeStep;
        _runner.BBCH_Susceptibility = BBCH_susceptibility;
        _runner.octoPusParameters = octoPusParameters;
        _runner.startYear = startYear;
        _runner.endYear = endYear;
        _runner.assistantRisk = assistantRisk;
        _runner.veryHighModelsThreshold = veryHighModelsThreshold;
        _runner.useLLM = useLLM;
        _runner.useRandomForest = useRandomForest;
        _runner.useConsole = useConsole;
        _runner.weatherDir = weatherDir;
        _runner.areEPIDMCASTexecutable = false;
        _runner.site_phenoParam_value = site_phenoParam;

        #region run phenology model
        double[,] resultsPheno = new double[1, 1];
        // DEBUG
        Console.WriteLine($"[DEBUG] paramCalibratedPheno = {paramCalibratedPheno}");
        Console.WriteLine($"[DEBUG] params = {string.Join(", ", calibratedParamNamesPheno)}");
        for (int d = 0; d < calibratedParamNamesPheno.Count; d++)
            Console.WriteLine($"[DEBUG]   {calibratedParamNamesPheno[d]}: [{LimitsPheno[d, 0]}, {LimitsPheno[d, 1]}]");
        msxPheno.Multistart(_runner, paramCalibratedPheno+ 1, LimitsPheno, out resultsPheno);

        var paramCalibValuePheno = new Dictionary<string, float>();
        int countPheno = 0;

        string headerPheno = "param, value";
        List<string> writeParamPheno = new List<string>();
        writeParamPheno.Add(headerPheno);
        foreach (var param in calibratedParamNamesPheno)
        {
            paramCalibValuePheno.Add(param, (float)resultsPheno[0, countPheno]);
            countPheno++;
        }

        // Monotonicita' BBCH: forza ogni anchor >= al precedente, prima di salvare il file
        var bbchKeysPheno = paramCalibValuePheno.Keys.Where(k => k.StartsWith("bbch")).OrderBy(k => int.Parse(k.Substring(4, 2))).ToList();
        for (int b = 1; b < bbchKeysPheno.Count; b++)
        {
            if (paramCalibValuePheno[bbchKeysPheno[b]] < paramCalibValuePheno[bbchKeysPheno[b - 1]])
                paramCalibValuePheno[bbchKeysPheno[b]] = paramCalibValuePheno[bbchKeysPheno[b - 1]];
        }

        // ricostruisci le righe del file con i valori corretti
        writeParamPheno = new List<string> { headerPheno };
        foreach (var param in calibratedParamNamesPheno)
            writeParamPheno.Add(param + "," + paramCalibValuePheno[param]);

        //write calibrated parameters to file
        System.IO.File.WriteAllLines("calibratedParametersPhenology//calibParam_" + siteKey + ".csv", writeParamPheno);

        //execute model with calibrated parameters
        _runner.oneShot(paramCalibValuePheno);
        #endregion
    }
    #endregion
}
else
{
    #region ONSET CALIBRATION (epidemiological models)

    //loop over clusters
    foreach (var cluster in refData.Keys)
    {
        #region read weather data
        //read weather data
        var weatherData = new Dictionary<string, Dictionary<DateTime, Input>>();
        WeatherReader weatherReader = new WeatherReader();
        foreach (var site in refData[cluster].Keys)
        {
            weatherData.Add(site, new Dictionary<DateTime, Input>());
            string weatherFile = Path.Combine(weatherDir, "weather_station_" + site + ".csv");
            switch (WeatherTimeStep)
            {
                case "hourly":
                    weatherData[site] = weatherReader.readHourly(weatherFile, startYear, endYear);
                    break;

                case "daily":
                    Dictionary<DateTime, InputDaily> weatherDataH = weatherReader.readDaily(weatherFile, startYear, endYear);
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

        var thisRefData = refData[cluster];

        foreach (var model in model_param_range.Keys)
        {
            _runner.nameParam = new Dictionary<string, ParameterRange>();
            if (modelsToRun.Contains(model))
            {
                //to avoid calibrating on parameter classes different from the eight 'octopus models'
                if (!toExclude.Contains(model))
                {
                    Console.WriteLine("Running calibration on cluster {0}\r", cluster);
                    #region define optimizer settings
                    // Multistart Nelder–Mead simplex configuration:
                    // - NofSimplexes: number of random starts
                    // - Ftol: tolerance on objective function for convergence
                    // - Itmax: maximum iterations per simplex
                    var msx = new MultiStartSimplex();
                    msx.NofSimplexes = 1;
                    msx.Ftol = 0.000000000001;
                    msx.Itmax = 1;

                    #endregion

                    #region Define parameter settings for calibration

                    // FIX 2: build a FRESH COPY of the parameter dictionary for this model
                    // (old code used a reference: Dictionary<string,ParameterRange> nameParam = model_param_range[model]
                    //  which caused an ArgumentException "duplicate key" at the 2nd model iteration)
                    var nameParam = new Dictionary<string, ParameterRange>(model_param_range[model]);
                    _runner.nameParam = nameParam;

                    //(A) CONDITION: IF YOU WANT TO CALIBRATE incubationDuration
                    //if (!nameParam.ContainsKey("incubationDuration"))
                    //{
                    //    nameParam["incubationDuration"] = new ParameterRange
                    //    {
                    //        min = 6,
                    //        max = 20,
                    //        calibration = "x"   // <-- calibrated
                    //    };
                    //}
                    //else
                    //{
                    //    nameParam["incubationDuration"].calibration = "x";
                    //}

                    //(B) CONDITION: IF YOU WANT TO FIX incubationDuration (not calibrated)
                    // Ensure incubationDuration exists with fixed bounds/value AND NEVER calibrate it
                    if (!nameParam.ContainsKey("incubationDuration"))
                    {
                        // se nel CSV non c'è, la definisci qui con valori fissi
                        nameParam["incubationDuration"] = new ParameterRange
                        {
                            min = 8,
                            max = 20,
                            //value = 15,          // <-- metti il default che vuoi
                            calibration = ""     // <-- NON calibrato
                        };
                    }
                    else
                    {
                        //    // se nel CSV c'è, forzi comunque che NON sia calibrato
                        nameParam["incubationDuration"].calibration = "";

                    }

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
                    double[,] Limits = new double[paramCalibrated + 1, 2];
                    for (int i = 0; i < calibratedParamNames.Count; i++)
                    {
                        var name = calibratedParamNames[i];
                        var param = nameParam[name];
                        Limits[i, 0] = param.min;
                        Limits[i, 1] = param.max;
                    }

                    //add incubation parameters

                    #endregion

                    //message to console
                    Console.WriteLine("CALIBRATION STARTED FOR MODEL {0}", model);

                    //set runner properties
                    _runner.availableSites = refData[cluster].Keys.ToList();
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
                    _runner.site_year_onsetDate = thisRefData;
                    _runner.cluster = cluster;
                    _runner.site_phenoParam_value = site_phenoParam;
                    _runner.weatherData = weatherData;
                    _runner.calibrationVariable = calibrationVariable;

                    #region run octoPus
                    //empty list of dates and SWELL outputs
                    var dateOutputs = new Dictionary<DateTime, OutputsDaily>();
                    // Run the multistart simplex optimizer
                    // Results buffer returned by the optimizer (1 row x N params here)
                    double[,] results = new double[1, 1];
                    msx.Multistart(_runner, paramCalibrated + 1, Limits, out results);

                    //get calibrated parameters
                    var paramCalibValue = new Dictionary<string, float>();
                    int count = 0;

                    #region write calibrated parameters
                    string header = "cluster, param, value";
                    List<string> writeParam = new List<string>();
                    writeParam.Add(header);
                    foreach (var param in calibratedParamNames)
                    {
                        //write a line for each parameter
                        string line = "";
                        line += cluster + ",";
                        line += param + ",";
                        line += results[0, count];
                        writeParam.Add(line);
                        paramCalibValue.Add(param, (float)results[0, count]);
                        count++;
                    }

                    //write calibrated parameters to file
                    System.IO.File.WriteAllLines("calibratedParameters//calibParam_" + cluster + "_" + model + ".csv", writeParam);
                    #endregion

                    //execute model with calibrated parameters
                    _runner.oneShot(paramCalibValue);
                    #endregion
                }
            }
        }
    }
    #endregion
}

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
    return (mod_par_ran);

}

public class ParameterRange
{
    public float min { get; set; }
    public float max { get; set; }
    public float value { get; set; }
    public string calibration { get; set; }
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