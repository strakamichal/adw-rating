/**
 * Renders a rating line chart with competition dots.
 * X-axis: fixed months for the selected period (1 or 2 years).
 * Line: rating progression. Dots: one per competition with name in tooltip.
 *
 * @param {string} canvasId - The DOM id of the <canvas> element.
 * @param {Array<{date: string, rating: number, competitionName: string}>} data
 *        Competition points (date as ISO yyyy-MM-dd, one entry per competition).
 * @param {number} years - How many years back to show (1 or 2).
 */
function initCompetitionTimeline(canvasId, data, years) {
    if (!data || data.length === 0) return;

    var ctx = document.getElementById(canvasId);
    if (!ctx) return;

    // Destroy existing chart if any
    var existing = Chart.getChart(ctx);
    if (existing) existing.destroy();

    // Time range
    var now = new Date();
    var startDate = new Date(now.getFullYear() - years, now.getMonth(), 1);
    var endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0);

    // Generate all months in range for labels
    var months = [];
    var d = new Date(startDate);
    while (d <= endDate) {
        months.push(new Date(d));
        d.setMonth(d.getMonth() + 1);
    }

    var monthLabels = months.map(function(m) {
        return m.toLocaleDateString('en-US', { month: 'short', year: '2-digit' });
    });

    // Parse data points and compute x position (fractional month index)
    function toMonthX(dateStr) {
        var parts = dateStr.split('-');
        var itemDate = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, parseInt(parts[2]));
        var monthIdx = (itemDate.getFullYear() - startDate.getFullYear()) * 12
            + itemDate.getMonth() - startDate.getMonth();
        var dayFraction = (itemDate.getDate() - 1) / 30;
        return monthIdx + dayFraction;
    }

    // Filter to range and build scatter points
    var points = [];
    data.forEach(function(item) {
        var x = toMonthX(item.date);
        if (x >= 0 && x < months.length) {
            points.push({
                x: x,
                y: item.rating,
                name: item.competitionName,
                dateStr: (function() {
                    var parts = item.date.split('-');
                    var d = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, parseInt(parts[2]));
                    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                })()
            });
        }
    });

    // Sort by x for the line
    points.sort(function(a, b) { return a.x - b.x; });

    // Build line data: extend from left edge to right edge
    var linePoints = [];
    if (points.length > 0) {
        // Extend to left edge with first competition's rating
        if (points[0].x > 0) {
            linePoints.push({ x: 0, y: points[0].y });
        }
        // All real points
        for (var i = 0; i < points.length; i++) {
            linePoints.push({ x: points[i].x, y: points[i].y });
        }
        // Extend to right edge with last competition's rating
        if (points[points.length - 1].x < months.length) {
            linePoints.push({ x: months.length, y: points[points.length - 1].y });
        }
    }

    new Chart(ctx, {
        type: 'scatter',
        data: {
            datasets: [
                // Line connecting dots (extended to edges)
                {
                    type: 'line',
                    data: linePoints,
                    borderColor: '#2c5f8a',
                    borderWidth: 2,
                    tension: 0.3,
                    fill: false,
                    pointRadius: 0,
                    pointHitRadius: 0,
                    showLine: true,
                    order: 2
                },
                // Competition dots
                {
                    data: points,
                    backgroundColor: '#2c5f8a',
                    borderColor: '#ffffff',
                    pointRadius: 6,
                    pointHoverRadius: 9,
                    pointBorderWidth: 2,
                    pointHoverBorderWidth: 2,
                    order: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'nearest',
                intersect: true
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    filter: function(item) {
                        return item.datasetIndex === 1;
                    },
                    callbacks: {
                        title: function(items) {
                            return items[0].raw.name;
                        },
                        label: function(context) {
                            return context.raw.dateStr + '  |  Rating: ' + Math.round(context.raw.y);
                        }
                    }
                }
            },
            scales: {
                x: {
                    type: 'linear',
                    min: 0,
                    max: months.length,
                    ticks: {
                        stepSize: 1,
                        callback: function(value) {
                            if (Number.isInteger(value) && value >= 0 && value < monthLabels.length) {
                                return monthLabels[value];
                            }
                            return '';
                        },
                        maxRotation: 45,
                        font: { size: 11 },
                        autoSkip: true,
                        maxTicksLimit: years === 1 ? 12 : 13
                    },
                    grid: { display: false }
                },
                y: {
                    beginAtZero: false,
                    ticks: {
                        font: { size: 11 }
                    }
                }
            }
        }
    });
}
