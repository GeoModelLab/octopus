using Models.Datatype;

namespace Models.Infections
{
    //Rule 3 10. Baldacci E (1947). Epiﬁtie di Plasmopara viticola (1941–46) nell’Oltrepó Pavese ed adozione del calendario di incubazione come strumento di lotta. Atti Ist Bot Lab Crittogam VIII:45–85
    public class Rule310
    {
        #region local variables
        List<Input> Past_24hours = new List<Input>();
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {
            //add one hour
            Past_24hours.Add(Input);
            //delete the hour at n hours
            if (Past_24hours.Count == Parameters.rule310Parameters.numberOfHoursToConsider)
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

            //TODO: not used here 
            double BBCH = 0;

            //The rule
            if (Precipitation >= Parameters.rule310Parameters.precipitationThreshold &&
                Temperature >= Parameters.rule310Parameters.baseTemperature &&
                BBCH >= Parameters.rule310Parameters.bbchThreshold)
            {
                var infectionEvent = new GenericInfection();
                infectionEvent.infectionDate = Input.Date;
                Output.outputsRule310.infectionEvents.Add(infectionEvent);
            }
        }
        #endregion
    }
}
