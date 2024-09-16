using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Microsoft Dynamics CRM namespace(s)
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.NPS.Rental.Project
{
    public class ForecastedInvoicesDates : IPlugin
    {
        DateTime billingDate_1;
        int noofmonths;
        OptionSetValue billingtype;
        Decimal amount = 0.00m;


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

                    if (entity.LogicalName == "bolt_rentalproject")
                    {
                        Entity ent = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));
                        if (ent.Attributes.Contains("bolt_expectednumberofbillingmonths"))
                        {
                             noofmonths = ent.GetAttributeValue<int>("bolt_expectednumberofbillingmonths");                            
                        }
                        if (ent.Attributes.Contains("bolt_1stbillingdate"))
                        {
                            billingDate_1 = ent.GetAttributeValue<DateTime>("bolt_1stbillingdate");
                        }
                        if(ent.Attributes.Contains("bolt_billingcycletype"))
                        {
                          billingtype = ent.GetAttributeValue<OptionSetValue>("bolt_billingcycletype");
                        }
                        if(ent.Attributes.Contains("bolt_reoccurringinvoiceamount"))
                        {
                            amount = ((Money)(ent.Attributes["bolt_reoccurringinvoiceamount"])).Value;
                        }
                      
                    }
                    if (billingDate_1 != null && noofmonths != 0 && billingtype.Value==454890002) //1st billing date, expected no.of months, billing type==monthly(454890002)
                    {
                        CalculateDate_MonthlyDates(billingDate_1, noofmonths, entity);
                    }
                    else if(billingDate_1 != null && billingtype.Value == 454890001)
                    {
                        CalculateDate_WeeklyDates(billingDate_1, entity);
                    }
                    else if(billingDate_1 != null &&  billingtype.Value == 454890000)
                    {
                        CalculateDate_DailyDates(billingDate_1, entity);
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace("RentalProjectForecastedInvoicesDates: {0}", ex.ToString());
                    throw;
                }
            }
        }

        public void CalculateDate_MonthlyDates(DateTime date_1 , int months, Entity rproject)
        {

            int days = 28;

            DateTime[] billingdates = new DateTime[12];

            for (int i=2;i<=months;i++)
            {

                billingdates[i] = date_1.AddDays(28 *(i - 1));

            }

            Entity e = new Entity();
            e.LogicalName = rproject.LogicalName;
            e.Id = rproject.Id;
            if (months >= 2)
            {
                e["bolt_2ndbillingdate"] = billingdates[2];
                e["bolt_2ndbillingamount"] = amount;
            }
            if (months >= 3)
            {
                e["bolt_3rdbillingdate"] = billingdates[3];
                e["bolt_3rdbillingamount"] = amount;
            }
            if (months >= 4)
            {
                e["bolt_4thbillingdate"] = billingdates[4];
                e["bolt_4thbillingamount"]= amount;
            }
            if (months >= 5)
            {
                e["bolt_5thbillingdate"] = billingdates[5];
                e["bolt_5thbillingamount"] = amount;
            }
            if (months >= 6)
            {
                e["bolt_6thbillingdate"] = billingdates[6];
                e["bolt_6thbillingamount"] = amount;
            }
            if (months >= 7)
            {
                e["bolt_7thbillingdate"] = billingdates[7];
                e["bolt_7thbillingamount"] = amount;
            }
            if (months >= 8)
            {
                e["bolt_8thbillingdate"] = billingdates[8];
                e["bolt_8thbillingamount"] = amount;
            }
            if (months >= 9)
            {
                e["bolt_9thbillingdate"] = billingdates[9];
                e["bolt_9thbillingamount"] = amount;
            }
            if (months >= 10)
            {
                e["bolt_10thbillingdate"] = billingdates[10];
                e["bolt_10thbillingamount"] = amount;
            }
            if (months >= 11)
            {
                e["bolt_11thbillingdate"] = billingdates[11];
                e["bolt_11thbillingamount"] = amount;
            }
            if (months >= 12)
            {
                e["bolt_12thbillingdate"] = billingdates[12];
                e["bolt_12thbillingamount"] = amount;
            }
            service.Update(e);

        }

        public void CalculateDate_WeeklyDates(DateTime date_1, Entity rproject)
        {
            Entity e = new Entity();
            e.LogicalName = rproject.LogicalName;
            e.Id = rproject.Id;
            e["bolt_2ndbillingdate"] = date_1.AddDays(7);
            e["bolt_2ndbillingamount"] = amount;
            service.Update(e);
        }
        public void CalculateDate_DailyDates(DateTime date_1, Entity rproject)
        {
            Entity e = new Entity();
            e.LogicalName = rproject.LogicalName;
            e.Id = rproject.Id;
            e["bolt_2ndbillingdate"] = date_1.AddDays(1);
            e["bolt_2ndbillingamount"] = amount;
            e["bolt_3rdbillingdate"] = date_1.AddDays(2);
            e["bolt_3rdbillingamount"] = amount;
            service.Update(e);

        }

    }
}
