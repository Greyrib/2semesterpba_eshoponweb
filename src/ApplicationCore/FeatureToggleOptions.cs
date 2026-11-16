using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore;

public class FeatureToggleOptions
{
    // Base class for the IOptions<T> that can be DI'd into classes (actual & unit tests)

    public bool UseCatalogMicroservice { get; set; }
}
