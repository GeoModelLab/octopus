using RDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace octoPusAI.ModelCallers
{
    internal class RInterface
    {

        // Define variables to store the R model and engine
        private REngine engine;
        private string modelPath = "octoPusAI.rds";  // Replace with the actual path
        private SymbolicExpression model;
        internal ModelOutputsML modelOutputsML = new ModelOutputsML();

        internal ModelOutputsML MLmodelCall(string Rversion)
        {
            // Set the folder containing R.dll
            if (engine == null)
            {
                string Rlocation = "c:/program files/R/";
                // Set the folder containing R.dll (path may vary based on your R installation)
                REngine.SetEnvironmentVariables(Rlocation + Rversion + "/bin/x64", Rlocation + Rversion);
                engine = REngine.GetInstance();

                // Workaround - explicitly include R libs in PATH so R environment can find them
                engine.Evaluate("Sys.setenv(PATH = paste(\"" + Rlocation + Rversion + "/bin/x64\", Sys.getenv('PATH'), sep=';'))");

            }
            try
            {
                // Initialize the R.NET engine if not already initialized
                if (!engine.IsRunning)
                {
                    engine.Initialize();

                }

                // Load the model only if it hasn't been loaded yet
                if (model == null)
                {
                    //Console.WriteLine("Loading the R model...");
                    engine.Evaluate($"model <- readRDS('{modelPath}')");
                    engine.Evaluate("library(stats)");
                    engine.Evaluate("if (!require('caret')) {install.packages('caret')}");
                    engine.Evaluate("if (!require('randomForest')) {install.packages('randomForest')}");
                    engine.Evaluate("library(caret)");
                }

                // Generate R code for data manipulation and prediction
                string Rcode = GenerateRCode(modelOutputsML);

                // Create the dataframe using R code
                engine.Evaluate("new_data <- " + Rcode);
                // model = engine.Evaluate("model");

                // Make predictions using the already loaded model
                engine.Evaluate("predictions <- predict(model, newdata=new_data)");

                // Fetch the predictions from R to C#
                var predictions = engine.Evaluate("predictions").AsNumeric();

                // Convert predictions to risk categories
                var risk = GetRisk(Math.Round(predictions[0], 2));

                //assign risk to modelOutputsML
                modelOutputsML.predictedRiskLabel = risk;
                modelOutputsML.predictedRisk = (float)Math.Round(predictions[0], 2);
            }
            finally
            {
                // Dispose of the engine when done
                //engine.Dispose();
            }
            return modelOutputsML;
        }

        // Function to generate R code string
        internal string GenerateRCode(ModelOutputsML model)
        {
            StringBuilder rCodeBuilder = new StringBuilder();

            // Add data frame creation
            rCodeBuilder.AppendLine("new_data <- data.frame(");

            // Add infection data
            foreach (var kvp in model.model_date_infection)
            {
                rCodeBuilder.AppendLine($"    {kvp.Key}mod = c({string.Join(", ", kvp.Value.Select(x => x.Value))}),");
            }

            // Add pressure data
            foreach (var kvp in model.model_date_pressure)
            {
                rCodeBuilder.AppendLine($"    {kvp.Key}cum = c({string.Join(", ", kvp.Value.Select(x => x.Value))}),");
            }

            // Add susceptibility and BBCH
            rCodeBuilder.AppendLine($"    plantMod = {model.susceptibility},");
            rCodeBuilder.AppendLine($"    BBCH = {model.BBCH}");

            // Close data frame creation
            rCodeBuilder.AppendLine(")");

            return rCodeBuilder.ToString();
        }

        internal string GetRisk(double predictions)
        {
            // Convert predictions to risk categories
            if (predictions <= 1.5)
                return "VERY LOW";
            else if (predictions <= 2.5)
                return "LOW";
            else if (predictions <= 3.5)
                return "MEDIUM";
            else if (predictions <= 4)
                return "HIGH";
            else
                return "VERY HIGH";
        }
    }

    internal class ModelOutputsML
    {
        internal Dictionary<string, Dictionary<DateTime, int>> model_date_infection = new Dictionary<string, Dictionary<DateTime, int>>();
        internal Dictionary<string, Dictionary<DateTime, int>> model_date_pressure = new Dictionary<string, Dictionary<DateTime, int>>();
        internal float susceptibility = 0;
        internal float BBCH = 0;
        internal string predictedRiskLabel = "";
        internal float predictedRisk = 0;
    }
}
