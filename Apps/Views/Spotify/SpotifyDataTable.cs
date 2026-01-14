using Ivy.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtistInsightTool.Apps.Views.Spotify;

public record SpotifyStreamData(string TrackName, int StreamCount, decimal EstimatedRevenue, string Country, DateTime Date);

public static class SpotifyDataTable
{
  public static object Create(IEnumerable<SpotifyStreamData> streams)
  {
    var tableData = streams.Select(s => new
    {
      s.TrackName,
      StreamCountDisplay = s.StreamCount.ToString("N0"),
      RevenueDisplay = s.EstimatedRevenue.ToString("C"),
      s.Country,
      DateDisplay = s.Date.ToShortDateString(),
      s.StreamCount, // For sorting if needed
      s.EstimatedRevenue // For sorting if needed
    }).ToArray();

    return tableData.ToTable()
         .Width(Size.Full())
         .Add(x => x.DateDisplay)
         .Add(x => x.TrackName)
         .Add(x => x.Country)
         .Add(x => x.StreamCountDisplay)
         .Add(x => x.RevenueDisplay)
         .Header(x => x.DateDisplay, "Date")
         .Header(x => x.TrackName, "Track")
         .Header(x => x.Country, "Country")
         .Header(x => x.StreamCountDisplay, "Streams")
         .Header(x => x.RevenueDisplay, "Est. Revenue");
  }
}
