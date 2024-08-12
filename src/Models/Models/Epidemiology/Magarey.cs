using Models.Datatype;

namespace Models.Infections
{
    //Magarey model - Magarey P, Wachtel M, Weir F, Seem R (1991). A computer-based simulator for rational management of grapevine downy mildew (Plasmopara viticola). Plant Prot Q 6, 29-33. DOI
    public class Magarey
    {
        #region local variables
        List<Input> Past_24hours = new List<Input>();
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {

            if (rule_210(Input, Parameters.magareyParameters) == 1)
            {
                var infectionEvent = new MagareyInfectionEvent();
                infectionEvent.germinationDate = Input.Date;
                Output.outputsMagarey.infectionEvents.Add(infectionEvent);
                infectionEvent.idInfection = Output.outputsMagarey.infectionEvents.Count();

            }

            foreach (var singleEvent in Output.outputsMagarey.infectionEvents)
            {
                var infectionEvent = singleEvent as MagareyInfectionEvent;

                #region Germination occurrence
                infectionEvent.GerminationHours += 1;
                if (infectionEvent.GerminationHours < Parameters.magareyParameters.numberOfHoursToConsiderGermination)
                {
                    infectionEvent.TemperatureAverageGerm += Input.Temperature /
                        Parameters.magareyParameters.numberOfHoursToConsiderGermination;
                    infectionEvent.RainSumGerm += Input.Precipitation;
                }
                else if (infectionEvent.GerminationHours == Parameters.magareyParameters.numberOfHoursToConsiderGermination)
                {
                    //verify germination occurrence
                    if (infectionEvent.TemperatureAverageGerm >= Parameters.magareyParameters.baseTemperatureInfection &&
                  infectionEvent.RainSumGerm >= Parameters.magareyParameters.precipitationThresholdInfection)
                    {
                        infectionEvent.Germination = 1;
                    }
                }
                #endregion

                #region Splashing occurrence
                //verify splashing occurrence
                if (infectionEvent.Germination == 1 && infectionEvent.Splashing == 0)
                {
                    infectionEvent.InfectionHours += 1;
                    infectionEvent.SplashingHours += 1;
                    if (infectionEvent.InfectionHours <= Parameters.magareyParameters.numberOfHoursToConsiderInfection)
                    {
                        infectionEvent.TemperatureAverageSplash += Input.Temperature;
                        infectionEvent.RainSumSplash = Input.Precipitation;

                        if (infectionEvent.RainSumSplash >= Parameters.magareyParameters.rainTriggeringSplash)
                        {
                            infectionEvent.TemperatureAverageSplash = infectionEvent.TemperatureAverageSplash / infectionEvent.InfectionHours;
                            if (infectionEvent.TemperatureAverageSplash >= Parameters.magareyParameters.baseTemperatureInfection)
                            {
                                infectionEvent.Splashing = 1;
                                infectionEvent.splashingDate = Input.Date;
                            }
                        }
                    }

                }
                #endregion

                #region Infection occurrence
                //Check infection occurrence
                if (infectionEvent.Splashing == 1)
                {
                    infectionEvent.InfectionHours += 1;
                    if (infectionEvent.InfectionHours <= Parameters.magareyParameters.numberOfHoursToConsiderInfection)
                    {
                        infectionEvent.DegreeHours += Input.Temperature;

                        if (infectionEvent.DegreeHours >= Parameters.magareyParameters.degreeHoursThresholdInfection)
                        {
                            infectionEvent.TemperatureAverageInf = infectionEvent.DegreeHours / infectionEvent.InfectionHours;

                            if (infectionEvent.InfectionHours <= Parameters.magareyParameters.numberOfHoursToConsiderInfection &&
                                infectionEvent.TemperatureAverageInf >= Parameters.magareyParameters.baseTemperatureInfection)
                            {
                                infectionEvent.Infection = 1;
                                infectionEvent.infectionDate = Input.Date;
                            }
                        }
                    }
                }
                #endregion
            }

            //remove uncompleted events
            Output.outputsMagarey.infectionEvents.RemoveAll(infectionEvent =>
            {
                var magareyInfection = infectionEvent as MagareyInfectionEvent;
                double elapsedHours = (Input.Date - magareyInfection.germinationDate).TotalHours;
                return elapsedHours > 24 && magareyInfection.Infection == 0;
            });
        }
        #endregion

        #region intermediate functions
        private int rule_210(Input Input, parametersMagarey magareyParameters)
        {
            int output = 0;
            //add one hour
            Past_24hours.Add(Input);
            //delete the hour at n hours
            if (Past_24hours.Count == magareyParameters.numberOfHoursToConsiderRule210)
            {
                Past_24hours.RemoveAt(0);
            }

            List<double> Precipitations = new List<double>();
            List<double> Temperatures = new List<double>();

            foreach (var gw in Past_24hours)
            {
                Precipitations.Add(gw.Precipitation);
                Temperatures.Add(gw.Temperature);
            }

            double Precipitation = Precipitations.Sum();
            double Temperature = Temperatures.Average();

            //Check conditions
            if (Precipitation >= magareyParameters.precipitationThresholdRule210 &&
                Temperature >= magareyParameters.baseTemperatureRule210)
            {
                output = 1;
            }
            return output;
        }
        #endregion


    }
}
