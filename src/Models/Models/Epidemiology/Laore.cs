using Models.Datatype;

namespace Models.Infections
{
    //Laore model - Reis EM, Sônego OR, Mendes CS (2013). Application and validation of a warning system for grapevine downy mildew control using fungicides. Summa Phytopathologica 39:10-15. https://doi.org/10.1590/S0100-54052013000100002
    public class Laore
    {
        #region local variables
        List<Input> Past_24hours = new List<Input>();
        List<double> TemperatureListForInfection = new List<double>();
        double CounterLWSporangia = 0;
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {
            //add one hour
            Past_24hours.Add(Input);
            //delete the hour at n hours

            if (Input.Date.Hour == 0)
            {
                TemperatureListForInfection = new List<double>();
                CounterLWSporangia = 0;
            }


            if (Input.Precipitation > 0 ||
                Input.LeafWetness == 1)
            {
                TemperatureListForInfection.Add(Input.Temperature);
                CounterLWSporangia++;
            }


            if (Input.Date.Hour == 23)
            {
                if (TemperatureListForInfection.Count > 0)
                {
                    double AvgT = TemperatureListForInfection.Average();
                    double CountLW = CounterLWSporangia;

                    double FirstFactor = -0.071 + 0.018 * AvgT - 0.0005 * AvgT * AvgT + 0.01;
                    double SecondFactor = 1 + (Math.Exp(-(0.24 * CountLW)) + 0.07 * AvgT * CountLW);
                    double ThirdFactor = 0.0021 * (AvgT * AvgT * CountLW) * -13.44;

                    double Risk = FirstFactor * (SecondFactor - ThirdFactor);
                    if (Risk < 0) Risk = 0;

                    Output.outputsLaore.dailyInfectionRisk = Risk;
                    if (Risk > Parameters.laoreParameters.infectionThresholdRisk)
                    {
                        var infectionEvent = new GenericInfection();
                        infectionEvent.infectionDate = Input.Date;
                        Output.outputsLaore.infectionEvents.Add(infectionEvent);
                    }
                }
            }
        }

        #endregion
    }
}
