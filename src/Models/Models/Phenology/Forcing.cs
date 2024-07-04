using Models.Datatype;

namespace Models.Phenology
{
    //Forcing model - Misfits group (2022). A public decision support system for the assessment of plant disease infection risk shared by Italian regions. J Environ Manage 1:317:115365 https://www.sciencedirect.com/science/article/pii/S0301479722009380?via%3Dihub
    public class Forcing
    {
        //run the forcing model
        public void runForcing(InputDaily Input, Parameters Parameters, Output Outputs)
        {
            //compute average temperature
            float averageTemperature = (Input.Tmax + Input.Tmin) / 2;

            //the forcing rate is computed only if the chill state is greater than 0 and the chill is started
            if (Outputs.outputsPhenology.chillState >= 0 && Outputs.outputsPhenology.isChillStarted)
            {
                //compute forcing rate
                Outputs.outputsPhenology.forcingRate = (Parameters.phenologyParameters.ToptPlant - Parameters.phenologyParameters.TminPlant) *
                    temperature_function(averageTemperature, Parameters.phenologyParameters.TmaxPlant, Parameters.phenologyParameters.TminPlant,
                    Parameters.phenologyParameters.ToptPlant);
                Outputs.outputsPhenology.forcingState += Outputs.outputsPhenology.forcingRate;
            }
            else
            {
                Outputs.outputsPhenology.forcingRate = 0;
                Outputs.outputsPhenology.forcingState = 0;
            }
            //compute cycle completion percentage
            Outputs.outputsPhenology.cycleCompletionPercentage = (Outputs.outputsPhenology.forcingState / 
                Parameters.phenologyParameters.CycleLength)*100;

            //check if the cycle is completed
            if(Outputs.outputsPhenology.cycleCompletionPercentage >= 100)
            {
                Outputs.outputsPhenology.cycleCompletionPercentage = 100;
                Outputs.outputsPhenology.forcingState = Parameters.phenologyParameters.CycleLength;
            }   

            //compute BBCH code
            computeBBCH(Parameters, Outputs);

            //update BBCH phenophase
            Outputs.outputsPhenology.bbchPhenophase = (int)Math.Truncate(Outputs.outputsPhenology.bbchPhenophaseCode);

            //compute host susceptibility
            phenologicalSusceptibility(Parameters, Outputs);
          
        }

        #region private methods
        private void computeBBCH(Parameters parameters, Output Outputs)
        {
            //get all keys
            List<int> keys = parameters.bbchParameters.Keys.AsEnumerable().ToList();


            float cumulatedGddThreshold = 0;
            //loop over them
            foreach (var _bbch in parameters.bbchParameters.Keys)
            {

                //NOTE: assumption here
                if (_bbch < 98)
                {
                    //this threshold 
                    cumulatedGddThreshold = parameters.bbchParameters[_bbch].cycleCompletion / 100 *
                            parameters.phenologyParameters.CycleLength;
                    //next threshold
                    float nextGddThreshold = cumulatedGddThreshold +
                        parameters.bbchParameters[_bbch + 1].cycleCompletion / 100 *
                        parameters.phenologyParameters.CycleLength;

                    if (Outputs.outputsPhenology.forcingState > cumulatedGddThreshold &&
                        Outputs.outputsPhenology.forcingState < nextGddThreshold)
                    {
                        Outputs.outputsPhenology.bbchPhenophaseCode = _bbch + (Outputs.outputsPhenology.forcingState - cumulatedGddThreshold) /
                            (nextGddThreshold - cumulatedGddThreshold);
                        //break;
                    }
                }
            }

        }

        public void phenologicalSusceptibility(Parameters parameters, Output Outputs)
        {
            float _phenoSusceptibility = 0;
            float BBCH = Outputs.outputsPhenology.bbchPhenophaseCode;
            if (BBCH > parameters.bbchSusceptibilityParameters.Keys.First() && BBCH < parameters.bbchSusceptibilityParameters.Keys.Last())
            {
                int x1 = parameters.bbchSusceptibilityParameters.Keys.Where(a => a < BBCH).OrderBy(a => a).Last();
                float y1 = parameters.bbchSusceptibilityParameters[Convert.ToInt32(x1)].susceptibility;
                int x2 = parameters.bbchSusceptibilityParameters.Keys.Where(a => a >= BBCH).OrderBy(a => a).First();
                float y2 = parameters.bbchSusceptibilityParameters[Convert.ToInt32(x2)].susceptibility;

                if ((x2 - x1) == 0)
                {
                    _phenoSusceptibility = (y2 + y1) / 2;
                }
                else
                {
                    _phenoSusceptibility = y1 + (BBCH - x1) * (y2 - y1) / (x2 - x1);
                }
            }
            else if (BBCH < parameters.bbchSusceptibilityParameters.Keys.First())
            {
                _phenoSusceptibility = parameters.bbchSusceptibilityParameters
                    [parameters.bbchSusceptibilityParameters.Keys.First()].susceptibility;
            }
            else if (BBCH > parameters.bbchSusceptibilityParameters.Keys.Last())
            {
                _phenoSusceptibility = parameters.bbchSusceptibilityParameters
                    [parameters.bbchSusceptibilityParameters.Keys.Last()].susceptibility;
            }

            Outputs.outputsPhenology.plantSusceptibility= _phenoSusceptibility;
        }

        private float temperature_function(float Temperature, float Tmax, float Tmin, float Topt)
        {
            float Tfunction = 0;

            if (Temperature < Tmin || Temperature > Tmax)
            {
                Tfunction = 0;
            }
            else
            {
                float firstTerm = (Tmax - Temperature) /
                        (Tmax - Topt);
                float secondTerm = (Temperature - Tmin) /
                                     (Topt - Tmin);
                float Exponential = (Topt - Tmin) /
                                     (Tmax - Topt);

                Tfunction = firstTerm * (float)Math.Pow(secondTerm, Exponential);
            }
            return Tfunction;
        }

        #endregion
    }
}
