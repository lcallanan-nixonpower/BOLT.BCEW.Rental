using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.NPS.Rental.CostSheetChildren
{
    public class TotalDayWeekMonthCalculation:IPlugin
    {
        /// <summary>
        /// A plugin that calculates the Total Days, Weeks, Months based on the Dispatched and Returned date fields
        /// </summary>
        /// <remarks>
        /// Entity: bolt_rentalgenerators (Rental Generators), bolt_rentalcables (Rental Cables)
        /// Message, Stage, Order, Mode: Create, PostOperation, 1, Synchronous, both entities
        /// Message, Stage, Order, Mode: Update, PostOperation, 1, Synchronous, Filters - bolt_cabledeparted, bolt_cablereturned
        /// Message, Stage, Order, Mode: Update, PostOperation, 1, Synchronous, Filters - bolt_dispatched, bolt_returned
        /// </remarks>

        public DateTime startdate;
        public DateTime returneddate;       
        
        int totaldays;
        int totalweeks;
        int totalmonths;
        decimal totalCost;

        IOrganizationService service;
        ITracingService tracingService;
        public void Execute(IServiceProvider serviceProvider)
        {

            //Extract the tracing service for use in debugging sandboxed plug-ins.
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                tracingService.Trace("A1");
                // Obtain the target entity from the input parmameters.
                Entity entity = (Entity)context.InputParameters["Target"];

                try
                {

                    tracingService.Trace("A2");
                    IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                    service = serviceFactory.CreateOrganizationService(context.UserId);

                    if (entity.LogicalName== "bolt_rentalgenerators")
                    {
                        Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        if (ent.Attributes.Contains("bolt_dispatched"))
                        {            
                            startdate = ent.GetAttributeValue<DateTime>("bolt_dispatched");
                        }
                        if (ent.Attributes.Contains("bolt_returned"))
                        {
                            returneddate = ent.GetAttributeValue<DateTime>("bolt_returned");
                        }
                        if (startdate != null && returneddate != null)
                        {
                            _calculate_noofDaysweeksmonths(ent);
                        }
                    }
                    else if(entity.LogicalName== "bolt_rentalcables")
                    {
                        Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        if (ent.Attributes.Contains("bolt_cabledeparted"))
                        {
                            startdate = ent.GetAttributeValue<DateTime>("bolt_cabledeparted");
                        }
                        if (ent.Attributes.Contains("bolt_cablereturned"))
                        {
                            returneddate = ent.GetAttributeValue<DateTime>("bolt_cablereturned");
                        }
                        if (startdate!=null&&returneddate!=null)
                        {
                            _calculate_noofDaysweeksmonths(ent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace("TotalStartupCostSheet: {0}", ex.ToString());
                    throw;
                }
            }
        
        }

        public void set_daysWeeksMonths(Entity em)
        {
            tracingService.Trace("5");
            Entity e = new Entity();
            e.LogicalName = em.LogicalName;
            e.Id = em.Id;
            e["bolt_totalmonths"] = totalmonths;
            e["bolt_totalweeks"] =totalweeks;
            e["bolt_totaldays"] =totaldays;
            service.Update(e);
                        
        }

        public void _calculate_noofDaysweeksmonths(Entity en)
        {            
            var noofDays = ((returneddate.Date - startdate.Date).Days) + 1;

            if (noofDays > 28)
            {
                decimal m = noofDays / 28;  // if no.of days between 17 & 28 will consider it as a month. (user requirement)

                totalmonths = ((int)(Math.Floor(m)));

                var d = noofDays - (28 * totalmonths);

                if (d == 0)
                {
                    totaldays = 0;
                    totalweeks = 0;
                }
                else if (d >= 1 && d < 3)
                {
                    totalweeks = 0;
                    totaldays = d;
                }
                else if (d >= 3 && d <= 7)
                {
                    totalweeks = 1;
                    totaldays = 0;
                }
                else if (d >= 10 && d <= 14)
                {
                    totaldays = 0;
                    totalweeks = 2;
                }
                else if (d >= 17 && d <= 28)
                {
                    totalmonths = totalmonths + 1;
                    totalweeks = 0;
                    totaldays = 0;
                }
                else if (d == 8 || d == 15)
                {
                    totaldays = 1;
                    if (d == 8)
                    {
                        totalweeks = 1;
                    }
                    else if (d == 15)
                    {
                        totalweeks = 2;
                    }
                }
                else if (d == 9 || d == 16)
                {
                    totaldays = 2;
                    if (d == 9)
                    {
                        totalweeks = 1;
                    }
                    else if (d == 16)
                    {
                        totalweeks = 2;
                    }
                }
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays >= 17 && noofDays <= 28)
            {
                totalmonths = 1;
                totalweeks = 0;
                totaldays = 0;

                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays >= 10 && noofDays < 15)
            {
                totalmonths = 0;
                totalweeks = 2;
                totaldays = 0;
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays >= 3 && noofDays <= 7)
            {
                totalmonths = 0;
                totalweeks = 1;
                totaldays = 0;
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays == 8 || noofDays == 15)
            {
                totalmonths = 0;
                totaldays = 1;
                if (noofDays == 8)
                {
                    totalweeks = 1;
                }
                else if (noofDays == 15)
                {
                    totalweeks = 2;
                }
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays == 9 || noofDays == 16)
            {
                totalmonths = 0;
                totaldays = 2;
                if (noofDays == 9)
                {
                    totalweeks = 1;
                }
                else if (noofDays == 16)
                {
                    totalweeks = 2;
                }
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays == 1)
            {
                totalmonths = 0;
                totalweeks = 0;
                totaldays = 1;
                //set no.of values
                set_daysWeeksMonths(en);
            }
            else if (noofDays == 2)
            {
                totalmonths = 0;
                totalweeks = 0;
                totaldays = 2;
                //set no.of values
                set_daysWeeksMonths(en);
            }
        }
       
    }
}
