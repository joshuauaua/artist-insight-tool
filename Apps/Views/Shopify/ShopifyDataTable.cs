using Ivy.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtistInsightTool.Apps.Views.Shopify;

public record ShopifySale(string SaleId, DateTime Date, string CustomerName, string Product, decimal Amount, string Status);

public static class ShopifyDataTable
{
  public static object Create(IEnumerable<ShopifySale> sales)
  {
    // Projecting to display items to ensure compatibility with ToTable pattern if needed, 
    // but trying to keep it simple. 
    // Following RevenueTableView pattern of explicit column definitions.

    var tableData = sales.Select(s => new
    {
      s.SaleId,
      DateDisplay = s.Date.ToShortDateString(),
      s.CustomerName,
      s.Product,
      AmountDisplay = s.Amount.ToString("C"),
      s.Status
    }).ToArray();

    return tableData.ToTable()
         .Width(Size.Full())
         .Add(x => x.SaleId)
         .Add(x => x.DateDisplay)
         .Add(x => x.CustomerName)
         .Add(x => x.Product)
         .Add(x => x.AmountDisplay)
         .Add(x => x.Status)
         .Header(x => x.SaleId, "Sale ID")
         .Header(x => x.DateDisplay, "Date")
         .Header(x => x.CustomerName, "Customer")
         .Header(x => x.Product, "Product")
         .Header(x => x.AmountDisplay, "Total")
         .Header(x => x.Status, "Status");
  }
}
