using Models.Datatype;

namespace Models.Phenology
{
    //Dormancy model - Cesaraccio C, Spano D, Snyder RL, Duce P (2004). Chilling and forcing model to predict bud-burst of crop and forest species. Agricultural and Forest Meteorology 126, 1:13.
    public class Dormancy
    {
        //run the dormancy model
        public void runDormancy(InputDaily Input, Parameters Parameters, Output Output)
        {
 
            //compute average temperature
            float averageTemperature = (Input.Tmax + Input.Tmin) / 2;

            //compute chill days (first condition)
            if (Input.Tmin > Parameters.phenologyParameters.ChillThreshold &&
                Input.Tmin >= 0)
            {
                Output.outputsPhenology.chillRate = 0;
                Output.outputsPhenology.antiChillRate = averageTemperature - 
                    Parameters.phenologyParameters.ChillThreshold;
            }
            //second condition
            else if (Input.Tmin >= 0 &&
                Input.Tmin <= Parameters.phenologyParameters.ChillThreshold &&
                Input.Tmax > Parameters.phenologyParameters.ChillThreshold)
            {
                float denominator = (2 * (Input.Tmax - Input.Tmin));

                Output.outputsPhenology.chillRate = -((averageTemperature - Input.Tmin) -
                    (float)Math.Pow(Input.Tmax - Parameters.phenologyParameters.ChillThreshold, 2) /denominator);

                Output.outputsPhenology.antiChillRate = (float)Math.Pow(Input.Tmax - Parameters.phenologyParameters.ChillThreshold, 2) /
                    denominator;

                if (!Output.outputsPhenology.isChillStarted && Output.outputsPhenology.chillRate<0)
                {
                    Output.outputsPhenology.isChillStarted = true;
                }
            }
            //third condition
            else if (Input.Tmin >= 0 && Input.Tmax <= Parameters.phenologyParameters.ChillThreshold)
            {
                Output.outputsPhenology.chillRate = -(averageTemperature - Input.Tmin);
                Output.outputsPhenology.antiChillRate = 0;

                if(!Output.outputsPhenology.isChillStarted)
                {
                    Output.outputsPhenology.isChillStarted = true;
                }
            }
            //fourth condition
            else if (Input.Tmin < 0 && Input.Tmax >= 0 && Input.Tmax <= Parameters.phenologyParameters.ChillThreshold)
            {
                Output.outputsPhenology.chillRate = -((float)Math.Pow(Input.Tmax, 2)) / (2 * (Input.Tmax - Input.Tmin));

                if (!Output.outputsPhenology.isChillStarted)
                {
                    Output.outputsPhenology.isChillStarted = true;
                }

                Output.outputsPhenology.antiChillRate = 0;
            }
            //fifth condition
            else if (Input.Tmin < 0 && Input.Tmax > Parameters.phenologyParameters.ChillThreshold)
            {
                float denominator = 2 * (Input.Tmax - Input.Tmin);

                Output.outputsPhenology.chillRate = -((float)Math.Pow(Input.Tmax, 2)) /
                   denominator - (float)Math.Pow(Input.Tmax - Parameters.phenologyParameters.ChillThreshold, 2) /
                    denominator;

                if (!Output.outputsPhenology.isChillStarted)
                {
                    Output.outputsPhenology.isChillStarted = true;
                }

                Output.outputsPhenology.antiChillRate = (float)Math.Pow(Input.Tmax - Parameters.phenologyParameters.ChillThreshold, 2) /
                    denominator;
            }
            else
            {
                Output.outputsPhenology.antiChillRate = 0;
                Output.outputsPhenology.chillRate = 0;
            }
           

            if (Output.outputsPhenology.chillState > -Parameters.phenologyParameters.ChillingRequirement &&
                Output.outputsPhenology.antiChillState==0)
            {
                Output.outputsPhenology.chillState = Output.outputsPhenology.chillState + Output.outputsPhenology.chillRate;
                Output.outputsPhenology.antiChillRate = 0;
                Output.outputsPhenology.antiChillState = 0;
            }
            else
            {
                Output.outputsPhenology.antiChillState = Output.outputsPhenology.antiChillState + Output.outputsPhenology.antiChillRate;
                Output.outputsPhenology.chillState = Output.outputsPhenology.chillState + Output.outputsPhenology.antiChillRate;
                if(Output.outputsPhenology.chillState > 0)
                {
                    Output.outputsPhenology.chillState= 0;
                }
            }

        }
    }
}
