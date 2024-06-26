using ChartJs.Blazor;
using ChartJs.Blazor.BarChart.Axes;
using ChartJs.Blazor.Common;
using ChartJs.Blazor.Common.Axes;
using ChartJs.Blazor.Common.Enums;
using ChartJs.Blazor.LineChart;

namespace GitHub.RepositoryExplorer.Client.Pages;

public partial class ProductLineChartJS : ComponentBase
{
    private ConfigureRepo? _config;
    private RepoLabels _repoLabelsState = new();
    private IList<IssuesSnapshot>? _issueSnapshots;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
    private DateOnly _endDate = DateOnly.FromDateTime(DateTime.Today);

    private IList<DateOnly> DateRange
    {
        get
        {
            List<DateOnly> dates = new();
            var date = _date;
            while (date < _endDate)
            {
                dates.Add(date);
                date = date.AddDays(1);
            }
            return dates;
        }
    }

    private Chart? _chart;

    private readonly LineConfig _chartConfig = new()
    {
        Options = new LineOptions
        {
            Responsive = true,
            Title = new OptionsTitle
            {
                Display = true,
                Text = "Open issues by product area"
            },
            Tooltips = new Tooltips
            {
                Mode = InteractionMode.Nearest,
                Intersect = true
            },
            Hover = new Hover
            {
                Mode = InteractionMode.Nearest,
                Intersect = true
            },
            Legend = new Legend
            {
                Display = true,
                Position = Position.Top
            },
            // This is a hack to get stacked line graphs.
            // We create scales, using the Stacked Bar Graph style
            // so we can set the "stacked" property in the underlying
            // Chart.JS library to "true".
            Scales = new Scales
            {
                XAxes = new List<CartesianAxis>
                {
                    new BarCategoryAxis
                    {
                        Stacked = true
                    }
                },
                YAxes = new List<CartesianAxis>
                {
                    new BarLinearCartesianAxis
                    {
                        Stacked = true
                    }
                }
            }
        }
    };

    [Inject]
    public AppInMemoryStateService AppState { get; set; } = null!;

    [Inject]
    public RepositoryLabelsClient RepositoryLabelsClient { get; set; } = null!;

    [Inject]
    public IssueSnapshotsClient SnapshotsClient { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await LoadSummaryDataAsync();
        }
    }

    private async Task LoadSummaryDataAsync()
    {
        if (AppState is { RepoState.IsAssigned: true })
        {
            var state = AppState.RepoState;
            var classifications = await RepositoryLabelsClient.GetRepositoryLabelsAsync(state);
            if (classifications is not null)
            {
                _repoLabelsState = _repoLabelsState with
                {
                    IsLoading = false,
                    IssueClassification = classifications
                };
                StateHasChanged();

                var data =
                    await SnapshotsClient.GetIssuesForDateRangeAsync(state, _date, _endDate, _repoLabelsState);
                _issueSnapshots = data?.ToList() ?? Array.Empty<IssuesSnapshot>().ToList();
                if (_issueSnapshots is { Count: > 0 })
                {
                    _chartConfig.Data.Labels.Clear();
                    _chartConfig.Data.XLabels.Clear();
                    foreach (var date in DateRange)
                    {
                        _chartConfig.Data.XLabels.Add(date.ToShortDateString());
                    }

                    _chartConfig.Data.Datasets.Clear();
                    foreach (var grouping in _issueSnapshots)
                    {
                        if (grouping.DailyCount.Any(i => i != 0 && i !=  -1))
                        {
                            double[] lineSeries = grouping.DailyCount
                                .Select(i => (i == -1) ? double.NaN : (double)i)
                                .ToArray();
                            _chartConfig.Data.Datasets.Add(
                                new LineDataset<double>(
                                    lineSeries)
                                {
                                    Fill = FillingMode.Start,
                                    Label = classifications.ProductWithUnassigned().First(p => p.Label == grouping.Product).DisplayLabel,
                                    BorderColor = ProductColor(grouping.Product!),
                                    BackgroundColor = ProductColor(grouping.Product!)
                                });
                        }
                    }

                    StateHasChanged();
                }
            }
        }
    }

    private Task OnLoadClick() => LoadSummaryDataAsync();

    private static string ProductColor(string productLabel) =>
        productLabel switch
        {
            ":star2: What's New" => "#7cfc00",
            "dotnet-fundamentals/prod" => "#9400D3",
            "dotnet-core/prod" => "#DDA0DD",
            "dotnet-architecture/prod" => "#a6fa33",
            "dotnet-csharp/prod" => "#228B22",
            "dotnet-fsharp/prod" => "#c4f750",
            "dotnet-visualbasic/prod" => "#dcf56c",
            "dotnet-api/prod" => "#edf388",
            "dotnet-desktop/prod" => "#f9f3a4",
            "dotnet-framework/prod" => "#fff4c0",
            "dotnet/prod" => "#fce3a6",
            "azure-dotnet/prod" => "#fcd28e",
            "dotnet-roslyn-api/prod" => "#fcd28e",
            "dotnet-data/prod" => "#fdac66",
            "dotnet-ml/prod" => "#fe9659",
            "dotnet-spark/prod" => "#ff7f50",
            _ => "#666600",
        };
}
