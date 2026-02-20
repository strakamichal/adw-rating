/**
 * Renders a rating progression line chart with sigma uncertainty band.
 * @param {string} canvasId - The DOM id of the <canvas> element.
 * @param {Array<{date: string, rating: number, sigma: number}>} data - Rating snapshots.
 * @param {number|null} peakRating - Peak rating value to mark, or null if same as current.
 */
function initRatingChart(canvasId, data, peakRating) {
    if (!data || data.length === 0) return;

    const ctx = document.getElementById(canvasId);
    if (!ctx) return;

    const labels = data.map(d => d.date);
    const ratings = data.map(d => d.rating);
    const upperBand = data.map(d => d.rating + d.sigma);
    const lowerBand = data.map(d => d.rating - d.sigma);

    // Find peak index if peak differs from last rating
    let peakIndex = -1;
    if (peakRating !== null && peakRating !== undefined) {
        const maxVal = Math.max(...ratings);
        peakIndex = ratings.indexOf(maxVal);
    }

    // Point radius: highlight peak with a larger dot
    const pointRadius = ratings.map((_, i) => i === peakIndex ? 8 : 4);
    const pointBackgroundColor = ratings.map((_, i) =>
        i === peakIndex ? '#d4950a' : '#2c5f8a'
    );

    new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Upper Band',
                    data: upperBand,
                    borderColor: 'transparent',
                    backgroundColor: 'rgba(44, 95, 138, 0.1)',
                    fill: '+1',
                    pointRadius: 0,
                    pointHitRadius: 0
                },
                {
                    label: 'Rating',
                    data: ratings,
                    borderColor: '#2c5f8a',
                    backgroundColor: '#2c5f8a',
                    fill: false,
                    tension: 0.3,
                    pointRadius: pointRadius,
                    pointBackgroundColor: pointBackgroundColor,
                    pointBorderColor: pointBackgroundColor,
                    borderWidth: 2
                },
                {
                    label: 'Lower Band',
                    data: lowerBand,
                    borderColor: 'transparent',
                    fill: false,
                    pointRadius: 0,
                    pointHitRadius: 0
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        title: function (items) {
                            return items[0].label;
                        },
                        label: function (context) {
                            if (context.datasetIndex === 1) {
                                const sigma = data[context.dataIndex].sigma;
                                return 'Rating: ' + Math.round(context.parsed.y) + ' (\u00b1' + Math.round(sigma) + ')';
                            }
                            return null;
                        }
                    },
                    filter: function (item) {
                        return item.datasetIndex === 1;
                    }
                }
            },
            scales: {
                x: {
                    type: 'category',
                    ticks: {
                        maxTicksLimit: 10,
                        font: { size: 11 }
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
