using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.NPS.Rental.Project
{
    public class UpdateAutomatedQuoteNumber : IPlugin
    {
        /// <summary>
        /// A plugin that updates the Rental Project.Quote # on create. The update adds the User defined initials to the Quote string, but only if the value is from the Autonumber. 
        /// I.e., substring exists '{-}', then the plugin will replace with user initials. 
        /// </summary>
        /// <remarks>
        /// Entity: bolt_rentalproject(Rental Project)
        /// Message: Create
        /// Stage: Pre Operation
        /// Mode: Synchronous
        /// Other: depends on the Autonumber field contain the substring "{-}'
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                //Entity entity = (Entity)context.InputParameters["Target"];

                // Obtain the IOrganizationService instance which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    // Create pointer to context target
                    Entity target = (Entity)context.InputParameters["Target"];

                    // Get User's initials from initiating user's record
                    Guid userId = context.InitiatingUserId;
                    string userInitials = service.Retrieve("systemuser", userId, new ColumnSet("bolt_initials")).GetAttributeValue<string>("bolt_initials");
                    
                    // If null or empty, just set value to a dash, indicating the plugin ran but did
                    if(userInitials is null || userInitials == ""){
                        userInitials = "-";
                    }

                    if (target.Contains("cr6f5_quotenumber"))
                    {
                        // Get Quote # value
                        string quote_num = target.GetAttributeValue<string>("cr6f5_quotenumber");

                        // Replace substring, "{-}", with user's initials
                        target["cr6f5_quotenumber"] = quote_num.Replace("{-}", userInitials);
                    }

                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in  plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}