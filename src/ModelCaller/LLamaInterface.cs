using LLama.Common;
using LLama;

namespace octoPusAI.ModelCallers
{
    //This class is the interface to the LLama model
    internal class LLamaInterface
    {
        //private properties
        public string modelPath;  // change it to your own model path in the octoPus.json file
        private LLamaWeights LLamaWeights; // The model weights
        private InteractiveExecutor executor; // The executor to run the Llama model
        private ModelParams parameters; // The LLama model parameters
        private InferenceParams InferenceParams; // The LLama inference parameters

        //Llama interface constructor to initialize the model
        public LLamaInterface(string modelPath)
        {
            InitializeLLama(modelPath);
        }

        //Initialize the LLama model
        private void InitializeLLama(string modelPath)
        {
            // Suppress console output during the LoadFromFile call
            var originalConsoleOut = Console.Out;
            var originalConsoleError = Console.Error;
            using (var stringWriterOut = new StringWriter())
            using (var stringWriterError = new StringWriter())
            {
                Console.SetOut(stringWriterOut);
                Console.SetError(stringWriterError);
                
                try
                {
                    parameters = new ModelParams(modelPath)
                    {
                        ContextSize = 4026, // The longest length of chat as memory.
                        //GpuLayerCount = 30, // How many layers to offload to GPU. Please adjust it according to your GPU memory.
                      
                    };
                    LLamaWeights = LLamaWeights.LoadFromFile(parameters);
                }
                finally
                {
                    Console.SetOut(originalConsoleOut);
                    Console.SetError(originalConsoleError);
                    //Meta - Llama - 3 - 8B - Instruct - correct - pre - tokenizer - and - EOS - token - Q8_0.gguf;
                }
            }


            using (var stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);
                Console.SetOut(originalConsoleOut);
            }

            
            var context = LLamaWeights.CreateContext(parameters);
            executor = new InteractiveExecutor(context);

            InferenceParams = new InferenceParams()
            {
                MaxTokens = 15000, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
                AntiPrompts = new List<string> { "<|eot_id|>" } // Stop generation once antiprompts appear.
            };
        }

        //messages to console
        public void consoleMessages(string site, DateTime date, ModelOutputsML mLmodel, int veryHighModelsTreshold, 
            out int infectionModelsToday, out string pressureText, out string infectionText)
        {

            infectionModelsToday =  mLmodel.model_date_infection
            .SelectMany(kv => kv.Value)
            .Where(kv => kv.Key == date && kv.Value == 1)
            .Count();


            // Use LINQ to extract the keys corresponding to value "1" for the specific date
            var infectionModelsNames = mLmodel.model_date_infection
                .Where(kv => kv.Value.ContainsKey(date) && kv.Value[date] == 1)
                .Select(kv => kv.Key)
                .ToList();

            var pressureModelsNames = mLmodel.model_date_pressure
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            infectionText = string.Join(" ", infectionModelsNames.Select(model => $"{model}"));
            
            pressureText = string.Join(" ", pressureModelsNames.Select(kv =>
            {
                var values = string.Join(", ", kv.Value.Select(innerKv => $"{innerKv.Value}"));
                return $"{kv.Key} {values}";
            }));

            #region change color of the console based on the infection risk 
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("     {0}", date.ToString("MMM dd yyyy"));

            if (mLmodel.predictedRiskLabel == "VERY LOW" && infectionModelsToday < veryHighModelsTreshold)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Beep(200,240);
                Console.WriteLine("");
                Console.WriteLine(" very low  | BBCH {0} SUSCEPTIBILITY {1}% RISK {2}",
                    Math.Round(mLmodel.BBCH, 0), Math.Round(mLmodel.susceptibility, 1), Math.Round(mLmodel.predictedRisk, 1));
                if (infectionModelsToday == 0)
                {
                    Console.WriteLine("  \\(^^)/   | PRESSURE {0}", pressureText);
                    Console.WriteLine("  ((()))   | no infection today");
                }
                else
                {
                    Console.WriteLine("  \\(**)/   | PRESSURE {0}", pressureText);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  ((()))   | INFECTIONS! {0}", infectionText);
                    Console.Beep(1000, 180);
                }
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("");
            }
            else if (mLmodel.predictedRiskLabel == "LOW" && infectionModelsToday < veryHighModelsTreshold)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Beep(300, 240);
                Console.WriteLine("");
                Console.WriteLine("   low     | BBCH {0} SUSCEPTIBILITY {1}% RISK {2}",
                    Math.Round(mLmodel.BBCH, 0), Math.Round(mLmodel.susceptibility, 1), Math.Round(mLmodel.predictedRisk, 1));
                if (infectionModelsToday == 0)
                {
                    Console.WriteLine("  ~(^^)~   | PRESSURE {0}", pressureText);
                    Console.WriteLine("  ((()))   | no infection today");
                }
                else
                {
                    Console.WriteLine("  ~(**)~   | PRESSURE {0}", pressureText);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  ((()))   | INFECTIONS! {0}", infectionText);
                    Console.Beep(1000, 180);
                }
                Console.ForegroundColor = ConsoleColor.Cyan;
                
                Console.WriteLine("");
            }
            else if (mLmodel.predictedRiskLabel == "MEDIUM" && infectionModelsToday < veryHighModelsTreshold)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Beep(400,240);
                Console.WriteLine("");
                Console.WriteLine("  medium    | BBCH {0} SUSCEPTIBILITY {1}% RISK {2}",
                Math.Round(mLmodel.BBCH, 0), Math.Round(mLmodel.susceptibility, 1), Math.Round(mLmodel.predictedRisk, 1));
                if (infectionModelsToday == 0)
                {
                    Console.WriteLine(" |_(°°)_|  | PRESSURE {0}", pressureText);
                    Console.WriteLine("   (())    | no infection today");
                }
                else
                {
                    Console.WriteLine(" |_(**)_|   | PRESSURE {0}", pressureText);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   (())     | INFECTIONS! {0}", infectionText);
                    Console.Beep(1000, 180);
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("");
            }
            else if (mLmodel.predictedRiskLabel == "HIGH" && infectionModelsToday < veryHighModelsTreshold)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Beep(500, 240);
                Console.WriteLine("");
                Console.WriteLine("   high    | BBCH {0} SUSCEPTIBILITY {1}% RISK {2}",
                    Math.Round(mLmodel.BBCH, 0), Math.Round(mLmodel.susceptibility, 1), Math.Round(mLmodel.predictedRisk, 1));
                if (infectionModelsToday == 0)
                {
                    Console.WriteLine("  _(°°)_   | PRESSURE {0}", pressureText);
                    Console.WriteLine("  //()\\\\   | no infection today");
                }
                else
                {
                    Console.WriteLine("  _(**)_   | PRESSURE {0}", pressureText);
                    Console.WriteLine("  //()\\\\   | INFECTIONS! {0}", infectionText);
                  Console.Beep(1000, 180);
                }
                Console.WriteLine("");
                
            }
            else if (mLmodel.predictedRiskLabel == "VERY HIGH" || infectionModelsToday >= veryHighModelsTreshold)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Beep(600, 240);
                Console.WriteLine("");
                Console.WriteLine(" very high | BBCH {0} SUSCEPTIBILITY {1}% RISK {2}",
                    Math.Round(mLmodel.BBCH, 0), Math.Round(mLmodel.susceptibility, 1), Math.Round(mLmodel.predictedRisk, 1));
                Console.Beep(1000, 180);
                Console.WriteLine("  _(--)_   | PRESSURE {0}", pressureText);
                Console.WriteLine("  ||||||   | INFECTIONS! {0}", infectionText);
                Console.WriteLine("");
            }
            #endregion
        }

        //this list stores the answers of the Llama assistant
        List<string> answers = new List<string>();

        //call the Llama model
        public async Task callLLama(string site, DateTime date, ModelOutputsML mLmodel, float assistantRisk, int riskModels)
        {
            //call console messages
            int infectionModelsCount = 0;
            string infectionText = "";
            string pressureText = "";
            consoleMessages(site, date, mLmodel, riskModels, out infectionModelsCount, out pressureText, out infectionText);

            //the assistant will answer only if the risk is higher than the assistantRisk variable or if the number of models that simulated an infection is higher than the risk models.
            //These parameters are set in the octoPus.json file
            if (mLmodel.predictedRisk > assistantRisk || infectionModelsCount > riskModels)
            {
                // Add chat histories as prompt to tell AI how to act.
                var chatHistory = new ChatHistory();

                //static message (this message is always the same and is informed by the analysis conducted with octoPus in Italy)
                chatHistory.AddMessage(AuthorRole.System, "You are octoPus, a decision support system for primary grapevine downy mildew " +
                    "(Plasmopara viticola). Your tentacles are an ensemble of eight infection models. " +
                    "Your eyes estimate grapevine phenology using the BBCH phase. " +                    
                    "Your brain is a Random Forest model trained on a reference dataset of risk " +
                    "using simulated seasonal infections and host susceptibility. It computes an infection risk from 1 (very low) to 5 (very high). "+ 
                    "Process-based models are UCSC, misfits, and Magarey: they adopt an hourly time step and parameters " +
                    "related to pathogen epidemiology. Empirical models are the 3-10 rule and Laore, which are based on temperature, " +
                    "rainfall and leaf wetness thresholds triggering infections. The models EPI, DM-Cast and UCSC consider the oospores survival process." +
                    " EPI and DM-Cast need daily historical series as input. " +
                    "The daily model outputs have been translated into a binary variable, 0 = no infection and 1 = infection. " +
                    "PREVIOUS KNOWLEDGE ON MODELS BEHAVIOR AND SIMILARITY IN ITALY:" +
                    "DMcast tends to simulate higher number of infections, followed by IPI > EPI > UCSC > Rule310 > Laore > Misfits > Magarey. " +
                    "Misfits and Magarey simulate much lower infections than the other models. " +
                    "In very conducive years, more than 15 infections indicate very high disease pressure." +
                    "EPI, IPI, UCSC, DMCast and Laore have behave similarly. They differ from Misfits, Magarey and Rule310 which behave similarly" +
                    "ADDITIONAL NOTES: " +
                    "Main grapevine phenology phases are: BBCH < 53 = leaf development, BBCH < 60: inflorescence emerge; BBCH < 70: flowering" +
                    " BBCH < 80: fruits development, BBCH < 90: ripening. " +
                    "Host susceptibility is given as a percentage and is estimated from BBCH based on expert-rules. " +
                    "You are smart, wise, nice, honest, and determined to promote Integrated Pest Management practices.");

                // Create a chat session
                ChatSession session = new(executor, chatHistory);
                string userInput = "";

                #region daily input message
                //inform the Llama assistant about the previous answer, the site, and the date
                if (answers.Count > 0)
                {
                    userInput += "this was your previous answer: " + answers.Last() + ". Please avoid repetitions.";
                }
                userInput += " I am in " + site + ", today is " + date.ToString("yyyy-MM-dd");
                int count = 0;
                foreach (var epimodel in mLmodel.model_date_infection)
                {
                    if (epimodel.Value.Values.First() == 1)
                    {
                        if (count == 0)
                        {
                            userInput += ". Today, these models simulated an infection: ";
                        }
                        userInput += epimodel.Key + " ";
                        count++;
                    }
                }
                userInput += ".This means that " + count + " models predicted an infection!";
                if (count == 0)
                {
                    userInput += ". No infections were simulated today by any model. ";
                }

                userInput += " The disease pressure (i.e., total number of simulated seasonal infections) is " + pressureText;
                
                string riskLabel = mLmodel.predictedRiskLabel;
                if(infectionModelsCount> riskModels)
                {
                    riskLabel = "VERY HIGH";
                }
                userInput += " the risk predicted by the Random Forest model is " + riskLabel + 
                    "and grapevine is in BBCH " + Math.Round(mLmodel.BBCH, 0);
                userInput += "Based on your knowledge on models behavior and today's outputs, elaborate farmers support for" +
                    "plant protection, and discuss the results of the models which simulated a primary infection. Then " +
                    "remind to consult regional extension services for further information. Do not make a chat, just speak in first person as octoPus." +
                    "Change text structure with respect to your previous answer";

                #endregion

                //send the message to the Console, waiting for the answer
                Console.WriteLine("ok, let me think....");
               
                //this variable stores the answer of the Llama assistant
                string message = "";
                await foreach (var text in session.ChatAsync(new ChatHistory.Message(AuthorRole.User, userInput), InferenceParams))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(text);
                    message += text;
                }

                // Save the message to a file
                string fileName = @"LlamaMessages//" + date.ToString("yyyy-MM-dd") + "_" + site + ".txt";
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.WriteLine(message);
                }
                answers.Add(message);
            }

            //wait for .2 second
            Thread.Sleep(0); // Pause for 1000 milliseconds (1 second)
        }

        public Task CallAsyncAndWaitOnResult(string site, DateTime date, ModelOutputsML mlModel, float assistantRisk, int veryHighModelsTreshold)
        {
            string siteShort = ExtractCityName(site);
            var task = callLLama(siteShort, date, mlModel, assistantRisk, veryHighModelsTreshold);
            task.Wait();
            return task;
        }

        static string ExtractCityName(string input)
        {
            // Find the last occurrence of the backslash and the period
            int lastBackslashIndex = input.LastIndexOf('\\');
            int lastDotIndex = input.LastIndexOf('.');

            // Extract the substring between the last backslash and the last dot
            if (lastBackslashIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastBackslashIndex)
            {
                return input.Substring(lastBackslashIndex + 1, lastDotIndex - lastBackslashIndex - 1);
            }

            return string.Empty; // Return empty string if the format is not as expected
        }
    }
}
