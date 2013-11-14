using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Source.Plugins
{
    /// <summary>
    /// When a Product is created, if there are Price Lists in the system matching the Product's Currency; automatically create a Price List Item for each.
    /// Pricing will be set to 100% of List Price.
    /// 
    /// Plugin Steps:
    /// create - product - post-operation - synchronous
    /// </summary>
    public class CreatePriceListItems : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);

            Entity target = context.InputParameters["Target"] as Entity;
            CreateListItems(target, service);
        }

        private void CreateListItems(Entity product, IOrganizationService service)
        {
            EntityReference defaultUnit = product.GetAttributeValue<EntityReference>("defaultuomid");
            EntityReference currency = product.GetAttributeValue<EntityReference>("transactioncurrencyid");

            // Get active Price Lists matching the Product's currency if set
            QueryExpression qe = new QueryExpression("pricelevel");
            qe.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0); // Active
            if (currency != null) { qe.Criteria.AddCondition("transactioncurrencyid", ConditionOperator.Equal, currency.Id); }

            EntityCollection priceLists = service.RetrieveMultiple(qe);
            if (priceLists != null && priceLists.Entities.Count > 0)
            {
                // Create a Price List Item for each Price List
                priceLists.Entities.ToList().ForEach(priceList =>
                {
                    Entity priceListItem = new Entity("productpricelevel");
                    priceListItem["pricingmethodcode"] = new OptionSetValue(2); // Percent of List
                    priceListItem["roundingpolicycode"] = new OptionSetValue(1); // None
                    priceListItem["percentage"] = 100M;
                    priceListItem["uomid"] = defaultUnit;
                    priceListItem["productid"] = new EntityReference("product", product.Id);
                    priceListItem["pricelevelid"] = new EntityReference("pricelevel", priceList.Id);

                    // Create each price list item
                    service.Create(priceListItem);
                });
            }
        }
    }
}
