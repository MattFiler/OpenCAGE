// dashboard_app.js (React with JSX)

const API_BASE_URL = './dashboard_api.php';

async function fetchData(action, params = {}) {
    const url = new URL(API_BASE_URL, window.location.href);
    url.searchParams.append('action', action);
    for (const key in params) url.searchParams.append(key, params[key]);
    const response = await fetch(url);
    if (!response.ok) {
        const errorData = await response.json().catch(() => ({ error: 'Network response error.' }));
        throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
    }
    return response.json();
}

function LoadingSpinner() {
    return React.createElement('div', { className: 'loading-spinner' },
        React.createElement('div', { className: 'spinner-border text-primary', role: 'status' },
            React.createElement('span', { className: 'sr-only' }, 'Loading...')
        )
    );
}

function ErrorMessage({ message }) {
    return React.createElement('div', { className: 'error-message alert alert-danger' }, message);
}

function BarChart({ chartId, data, label, title, yAxisLabel, xAxisLabel = 'Version', averageValue = null }) {
    const chartRef = React.useRef(null);
    const chartInstanceRef = React.useRef(null);

    React.useEffect(() => {
        if (chartRef.current && data && data.labels && data.values) {
            if (chartInstanceRef.current) chartInstanceRef.current.destroy();
            
            const annotations = {};
            if (averageValue !== null && averageValue > 0) {
                annotations.averageLine = {
                    type: 'line',
                    yMin: averageValue,
                    yMax: averageValue,
                    borderColor: 'rgba(255, 99, 132, 0.5)', 
                    borderWidth: 2,
                    borderDash: [6, 6], 
                    label: {
                        content: `Avg: ${averageValue.toFixed(1)}`,
                        enabled: true,
                        position: 'end', 
                        backgroundColor: 'rgba(255, 99, 132, 0.5)',
                        font: { style: 'italic', size: 10 }, 
                        color: 'white',
                        padding: 3,
                        yAdjust: -10
                    }
                };
            }

            const ctx = chartRef.current.getContext('2d');
            chartInstanceRef.current = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: data.labels,
                    datasets: [{
                        label: label, data: data.values, backgroundColor: 'rgba(0, 123, 255, 0.6)',
                        borderColor: 'rgba(0, 123, 255, 1)', borderWidth: 1, borderRadius: 4,
                    }]
                },
                options: {
                    responsive: true, maintainAspectRatio: false,
                    plugins: {
                        legend: { display: true, position: 'top' },
                        title: { display: !!title, text: title, font: { size: 16 } },
                        tooltip: { callbacks: { title: (tooltipItems) => `Version: ${tooltipItems[0].label}` } },
                        annotation: { annotations: annotations } 
                    },
                    scales: {
                        y: { beginAtZero: true, title: { display: true, text: yAxisLabel } },
                        x: { title: { display: true, text: xAxisLabel } }
                    }
                }
            });
        }
        return () => { if (chartInstanceRef.current) chartInstanceRef.current.destroy(); };
    }, [data, label, title, yAxisLabel, xAxisLabel, averageValue]);

    if (!data || !data.labels || !data.values) return React.createElement(LoadingSpinner);
    if (data.labels.length === 0) return React.createElement('p', {className: 'text-center text-muted'}, 'No data available.');
    return React.createElement('canvas', { ref: chartRef, id: chartId, style: {minHeight: '350px', maxHeight: '450px'} });
}

function ScatterChart({ chartId, datasets, title, xAxisLabel, yAxisLabel, xAxisType = 'linear', yAxesConfig = {}, reverseXAxis = false, customAnnotations = {} }) {
    const chartRef = React.useRef(null);
    const chartInstanceRef = React.useRef(null);

    React.useEffect(() => {
        if (chartRef.current && datasets && datasets.length > 0 && datasets.some(ds => ds.data && ds.data.length > 0)) {
            if (chartInstanceRef.current) chartInstanceRef.current.destroy();
            
            const defaultYAxis = {
                id: 'yPrimary', type: 'linear', position: 'left', display: true, 
                beginAtZero: true, 
                title: { display: true, text: yAxisLabel || 'Primary Value' } 
            };
            if (chartId === 'releaseTimelineChart') { defaultYAxis.display = false; defaultYAxis.title.display = false; }

            const scales = {
                x: {
                    type: xAxisType, title: { display: true, text: xAxisLabel },
                    time: xAxisType === 'time' ? { unit: 'month', tooltipFormat: 'DD MMM YYYY' } : undefined, 
                    ticks: xAxisType === 'time' ? { autoSkip: true, maxTicksLimit: 12, major: { enabled: true } } : undefined,
                    reverse: reverseXAxis 
                },
                yPrimary: defaultYAxis 
            };
            Object.assign(scales, yAxesConfig); 
            if (!scales.yPrimary || scales.yPrimary.beginAtZero === undefined) {
                 scales.yPrimary = { ...defaultYAxis, ...scales.yPrimary, beginAtZero: true };
             }
             for (const key in scales) {
                 if (key !== 'x' && key !== 'yPrimary' && scales[key].type !== 'category' && scales[key].beginAtZero === undefined) {
                     scales[key].beginAtZero = true;
                 }
             }

            const ctx = chartRef.current.getContext('2d');
            chartInstanceRef.current = new Chart(ctx, {
                type: 'scatter', 
                data: { datasets: datasets }, 
                options: {
                    responsive: true, maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false }, 
                    plugins: {
                        legend: { 
                            display: true, 
                            position: 'top',
                            onClick: (e, legendItem, legend) => { 
                                const index = legendItem.datasetIndex;
                                const ci = legend.chart;
                                if (ci.isDatasetVisible(index)) {
                                    ci.hide(index);
                                    legendItem.hidden = true;
                                } else {
                                    ci.show(index);
                                    legendItem.hidden = false;
                                }
                            }
                        },
                        title: { display: !!title, text: title, font: { size: 16 } },
                        tooltip: { 
                             callbacks: { 
                                title: function(tooltipItems) { 
                                    const firstItem = tooltipItems[0];
                                    if (!firstItem || !firstItem.raw) return ''; 
                                    const point = firstItem.raw;
                                    
                                    if (point.version && chartId === 'popularityChart') return `Version: ${point.version}`;
                                    if (xAxisType === 'time' && point.originalReleaseDate && chartId === 'releaseTimelineChart' && firstItem.dataset.label === 'Release Event') {
                                        return point.originalReleaseDate; 
                                    }
                                    if (xAxisType === 'time' && point) return moment(point.x).format('Do MMMM YYYY'); 
                                    return `${xAxisLabel}: ${firstItem.parsed.x}`; 
                                },
                                label: function(context) {
                                    if (!context || !context.raw) return ''; 
                                    const point = context.raw;
                                    const datasetLabel = context.dataset.label || '';
                                    let tooltipLines = [];

                                    if (chartId === 'popularityChart') {
                                        if (datasetLabel === 'Trend') return null; 
                                        tooltipLines.push(`Popularity Score: ${point.y !== undefined ? point.y.toFixed(3) : 'N/A'}`);
                                        if (point.totalUsers !== undefined) tooltipLines.push(`Total Users: ${point.totalUsers}`);
                                        if (point.totalSessions !== undefined) tooltipLines.push(`Total Sessions: ${point.totalSessions}`);
                                        if (point.daysAvailable !== undefined) tooltipLines.push(`Days Available: ${point.daysAvailable}`);
                                        if (point.daysSincePreviousRelease !== undefined && point.daysSincePreviousRelease !== null) {
                                            tooltipLines.push(`Days Since Prev. Release: ${point.daysSincePreviousRelease}`);
                                        } else if (point.daysSincePreviousRelease === null) {
                                            tooltipLines.push(`Days Since Prev. Release: N/A (First)`);
                                        }
                                        if (point.originalReleaseDate) tooltipLines.push(`Released: ${point.originalReleaseDate}`);
                                    } else if (chartId === 'releaseTimelineChart') {
                                        if (datasetLabel === 'Release Event' && point.version) {
                                            tooltipLines.push(`Version: ${point.version}`);
                                            if (point.daysAvailable !== undefined) tooltipLines.push(`Days Available: ${point.daysAvailable}`);
                                            if (point.daysSincePreviousRelease !== undefined && point.daysSincePreviousRelease !== null) {
                                                tooltipLines.push(`Days Since Prev. Release: ${point.daysSincePreviousRelease}`);
                                            } else if (point.daysSincePreviousRelease === null) {
                                                 tooltipLines.push(`Days Since Prev. Release: N/A (First)`);
                                            }
                                        } else { 
                                            const chart = context.chart;
                                            const datasetMeta = chart.getDatasetMeta(context.datasetIndex);
                                            if(datasetMeta.visible) {
                                                tooltipLines.push(`${datasetLabel}: ${context.parsed.y.toFixed(0)}`);
                                                if (point.version) tooltipLines.push(`Version: ${point.version}`);
                                            } else {
                                                return null; 
                                            }
                                        }
                                    } else { 
                                        tooltipLines.push(`${datasetLabel}: ${context.parsed.y.toFixed(point.y % 1 !== 0 ? 2 : 0)}`);
                                        if (point.version) tooltipLines.push(`Version: ${point.version}`);
                                    }
                                    return tooltipLines;
                                }
                            }
                        },
                        annotation: { 
                            annotations: customAnnotations 
                         }
                    },
                    scales: scales
                }
            });
        }
        return () => { if (chartInstanceRef.current) chartInstanceRef.current.destroy(); };
    }, [datasets, title, xAxisLabel, yAxisLabel, xAxisType, yAxesConfig, reverseXAxis, chartId, customAnnotations]); 

    if (!datasets || datasets.length === 0) return React.createElement(LoadingSpinner);
    const hasData = datasets.some(ds => ds.data && ds.data.length > 0);
    if (!hasData) return React.createElement('p', {className: 'text-center text-muted'}, 'No data available.');
    
    return React.createElement('canvas', { ref: chartRef, id: chartId, style: {minHeight: '350px', maxHeight: '450px'} });
}

function HeatmapTimeline({ data }) { 
     if (!data) return React.createElement(LoadingSpinner);
    if (data.length === 0) return React.createElement('p', {className: 'text-center text-muted mt-3'}, 'No release data for heatmap.');

    const baseRed = 220, baseGreen = 50, baseBlue = 50; 
    const displayData = [...data].reverse(); 

    return React.createElement('div', { className: 'heatmap-timeline-container' },
        displayData.map(item => { 
            const green = Math.floor(baseGreen + (230 - baseGreen) * (1 - item.intensity)); 
            const blue = Math.floor(baseBlue + (230 - baseBlue) * (1 - item.intensity));   
            const color = `rgb(${baseRed}, ${green}, ${blue})`;
            const textColor = item.intensity > 0.5 ? 'white' : '#333';

            return React.createElement('div', {
                key: item.isoMonthYear, className: 'heatmap-segment',
                style: { backgroundColor: color, color: textColor },
                title: `${item.month} ${item.year}: ${item.releaseCount} release(s)`
            },
                React.createElement('div', { className: 'heatmap-month' }, item.month),
                React.createElement('div', { className: 'heatmap-year' }, item.year)
            );
        })
    );
}

function TopUsersList({ data }) {
    if (!data) return React.createElement(LoadingSpinner);
    if (data.length === 0) return React.createElement('p', {className: 'text-center text-muted mt-3'}, 'No user data.');

    const getRankIcon = (rank) => {
        if (rank === 1) return '🏆 '; if (rank === 2) return '🥈 '; if (rank === 3) return '🥉 ';
        return `${rank}. `;
    };

    return React.createElement('ul', { className: 'list-group list-group-flush' }, 
        data.map(user => React.createElement('li', {
            key: user.clientId, className: 'list-group-item d-flex justify-content-between align-items-center'
        },
            React.createElement('div', null, 
                React.createElement('span', {style: {fontWeight: user.rank <=3 ? 'bold' : 'normal', fontSize: user.rank <=3 ? '1.1em': '1em'}}, getRankIcon(user.rank)),
                `Client ID: ${user.clientId}`
            ),
            React.createElement('div', { className: 'text-right' }, 
                React.createElement('span', { className: 'badge badge-info badge-pill mr-2' }, `${user.totalSessions} sessions`),
                React.createElement('span', { 
                    className: 'badge badge-secondary badge-pill top-user-versions-tooltip',
                    title: (user.versionsUsedList && Array.isArray(user.versionsUsedList) && user.versionsUsedList.length > 0)
                           ? user.versionsUsedList.join('\n') 
                           : 'No versions recorded'
                }, `${user.versionsUsedCount} versions`)
            )
        ))
    );
}

function StatItem({ label, value, isLoading, valueFontSize = '1.5rem' }) {
    if (isLoading) return React.createElement('div', {className: 'text-center py-2'}, React.createElement(LoadingSpinner));
    if (value === null || value === undefined || value === '') return React.createElement('div', {className: 'text-center py-2 text-muted'}, label, ': N/A');
    return React.createElement('div', { className: 'text-center px-2 py-1' }, 
        React.createElement('div', { className: 'stat-value', style: { fontSize: valueFontSize, color: '#007bff', fontWeight: '600' } }, value),
        React.createElement('div', { className: 'stat-label', style: { fontSize: '0.85rem', color: '#6c757d'} }, label)
    );
}

// --- ChartCard Component ---
function ChartCard({ chartId, title, children, isExpanded, onToggleExpand, isLoading, error, headerControls = null }) { 
    return React.createElement(
        'div', 
        { className: `mb-4 ${isExpanded ? 'col-md-12' : 'col-md-6'}` }, 
        React.createElement(
            'div', 
            { className: 'card h-100' }, 
            React.createElement(
                'div', 
                { className: 'card-header d-flex justify-content-between align-items-center' },
                React.createElement('span', null, title), 
                React.createElement('div', { className: 'd-flex align-items-center' }, 
                     headerControls, 
                     React.createElement(
                        'button', 
                        { 
                            className: 'btn btn-sm btn-outline-light py-0 px-1 ml-2', 
                            onClick: () => onToggleExpand(chartId),
                            title: isExpanded ? 'Collapse Chart' : 'Expand Chart',
                            style: { lineHeight: 1 } 
                        },
                        isExpanded ? '−' : '+' 
                    )
                )
            ),
            React.createElement(
                'div', 
                { className: 'card-body chart-container', style: { position: 'relative' } }, 
                isLoading ? React.createElement(LoadingSpinner) :
                error ? React.createElement(ErrorMessage, { message: error }) :
                children 
            )
        )
    );
}


function App() {
    // State definitions
    const [overallStats, setOverallStats] = React.useState({ totalUniqueUsers: null, totalSessions: null, totalVersions: null });
    const [latestVersionStats, setLatestVersionStats] = React.useState({ 
        versionNumber: null, releaseDate: null, daysAgo: null, sessions: null, users: null
    });
    const [statsPerVersionData, setStatsPerVersionData] = React.useState({ stats: [], averageSessions: 0, averageUniqueUsers: 0 });
    const [allClientData, setAllClientData] = React.useState([]); 
    const [selectedClientId, setSelectedClientId] = React.useState('');
    const [userHistory, setUserHistory] = React.useState(null); 
    const [releaseTimelineAPIData, setReleaseTimelineAPIData] = React.useState(null); 
    const [popularityAPIData, setPopularityAPIData] = React.useState({ data: [], trendline: null }); 
    const [releaseHeatmapData, setReleaseHeatmapData] = React.useState(null);
    const [topUsersData, setTopUsersData] = React.useState(null);
    const [expandedChartId, setExpandedChartId] = React.useState(null); 
    const [useSteppedLine, setUseSteppedLine] = React.useState(true); 
    const [showEvents, setShowEvents] = React.useState(true); 

    const [loading, setLoading] = React.useState({
        overall: true, latestVersion: true, 
        versionStats: true, clientIds: true, userHistory: false, 
        timeline: true, popularity: true, heatmap: true, topUsers: true
    });
    const [error, setError] = React.useState({
        overall: null, latestVersion: null, 
        versionStats: null, clientIds: null, userHistory: null, 
        timeline: null, popularity: null, heatmap: null, topUsers: null
    });

    // Fetching data useEffect
    React.useEffect(() => {
        fetchData('getOverallStats').then(setOverallStats).catch(e => setError(p => ({...p, overall:e.message}))).finally(()=>setLoading(p=>({...p,overall:false})));
        fetchData('getLatestVersionStats').then(setLatestVersionStats).catch(e=>setError(p=>({...p,latestVersion:e.message}))).finally(()=>setLoading(p=>({...p,latestVersion:false}))); 
        fetchData('getStatsPerVersion').then(setStatsPerVersionData).catch(e=>setError(p=>({...p,versionStats:e.message}))).finally(()=>setLoading(p=>({...p,versionStats:false})));
        fetchData('getAllClientIds').then(setAllClientData).catch(e=>setError(p=>({...p,clientIds:e.message}))).finally(()=>setLoading(p=>({...p,clientIds:false}))); 
        fetchData('getReleaseTimeline').then(setReleaseTimelineAPIData).catch(e=>setError(p=>({...p,timeline:e.message}))).finally(()=>setLoading(p=>({...p,timeline:false})));
        fetchData('getPopularityData').then(setPopularityAPIData).catch(e=>setError(p=>({...p,popularity:e.message}))).finally(()=>setLoading(p=>({...p,popularity:false})));
        fetchData('getReleaseHeatmapData').then(setReleaseHeatmapData).catch(e=>setError(p=>({...p,heatmap:e.message}))).finally(()=>setLoading(p=>({...p,heatmap:false})));
        fetchData('getTopUsers').then(setTopUsersData).catch(e=>setError(p=>({...p,topUsers:e.message}))).finally(()=>setLoading(p=>({...p,topUsers:false})));
    }, []);

    // User history useEffect
    React.useEffect(() => {
        if (selectedClientId) {
            setLoading(p => ({ ...p, userHistory: true })); setUserHistory(null);
            fetchData('getUserHistory', { clientId: selectedClientId })
                .then(setUserHistory).catch(e=>setError(p=>({...p,userHistory:e.message}))).finally(()=>setLoading(p=>({...p,userHistory:false})));
        } else { setUserHistory(null); }
    }, [selectedClientId]);

    // --- Handlers ---
    const handleClientSelect = (event) => setSelectedClientId(event.target.value);
    const handleToggleExpand = (chartId) => {
        setExpandedChartId(prev => (prev === chartId ? null : chartId));
    };
    const toggleSteppedLine = () => setUseSteppedLine(prev => !prev); // Handler for stepped line
    const toggleShowEvents = () => setShowEvents(prev => !prev); 

    // --- Memoized Data Processing ---
    const sessionsPerVersionChartData = React.useMemo(() => { 
        if (!statsPerVersionData || !statsPerVersionData.stats) return null;
        return { labels: statsPerVersionData.stats.map(s => s.version), values: statsPerVersionData.stats.map(s => s.totalSessions) };
    }, [statsPerVersionData]);
    const uniqueUsersPerVersionChartData = React.useMemo(() => { 
        if (!statsPerVersionData || !statsPerVersionData.stats) return null;
        return { labels: statsPerVersionData.stats.map(s => s.version), values: statsPerVersionData.stats.map(s => s.uniqueUsers) };
    }, [statsPerVersionData]);
    
    const releaseTimelineDatasets = React.useMemo(() => { 
        if (!releaseTimelineAPIData) return []; 
        const datasets = [];
        datasets.push({
            label: 'Release Event',
            data: releaseTimelineAPIData.map((item, index) => ({
                x: item.releaseDate, y: index, version: item.version, 
                originalReleaseDate: item.originalReleaseDate,
                daysAvailable: item.daysAvailable, daysSincePreviousRelease: item.daysSincePreviousRelease
            })),
            backgroundColor: 'rgba(40, 167, 69, 0.7)', pointRadius: 6, yAxisID: 'yPrimary' 
        });
        datasets.push({
            label: 'Sessions',
            data: releaseTimelineAPIData.map(item => ({ x: item.releaseDate, y: item.sessions, version: item.version })),
            borderColor: 'rgba(255, 159, 64, 0.8)', backgroundColor: 'rgba(255, 159, 64, 0.5)',
            type: 'line', tension: 0.1, fill: false, yAxisID: 'yOverlays', hidden: true 
        });
        datasets.push({
            label: 'Unique Users',
            data: releaseTimelineAPIData.map(item => ({ x: item.releaseDate, y: item.users, version: item.version })),
            borderColor: 'rgba(54, 162, 235, 0.8)', backgroundColor: 'rgba(54, 162, 235, 0.5)',
            type: 'line', tension: 0.1, fill: false, yAxisID: 'yOverlays', hidden: true 
        });
        return datasets;
    }, [releaseTimelineAPIData]); 

    const releaseTimelineYAxesConfig = React.useMemo(() => { 
        const config = {};
        if (releaseTimelineAPIData && releaseTimelineAPIData.some(item => item.sessions > 0 || item.users > 0)) {
             config.yOverlays = {
                type: 'linear', position: 'right', beginAtZero: true,
                grid: { drawOnChartArea: false }, title: { display: true, text: 'Count (Sessions/Users)' }
            };
        }
        return config;
    }, [releaseTimelineAPIData]); 

    // --- Popularity Chart Dataset with Trendline & Stepped Line Toggle ---
    const popularityChartDatasets = React.useMemo(() => { 
        if (!popularityAPIData || !popularityAPIData.data) return []; 
        const datasets = [];
        
        datasets.push({ 
            label: 'Popularity Score', 
            data: popularityAPIData.data.map(item => ({
                x: item.releaseDate, y: item.popularityScore, version: item.version,
                totalUsers: item.totalUsers, totalSessions: item.totalSessions, 
                daysSincePreviousRelease: item.daysSincePreviousRelease, daysAvailable: item.daysAvailable, 
                originalReleaseDate: item.originalReleaseDate, releaseDate: item.releaseDate 
            })),
            backgroundColor: 'rgba(255, 193, 7, 0.7)', borderColor: 'rgba(255, 193, 7, 1)',
            showLine: true, 
            stepped: useSteppedLine ? 'before' : false, // Use stepped property
            tension: 0, // Ensure tension is 0 for stepped/straight lines
            pointRadius: 5, yAxisID: 'yPrimary' 
        });
        
        const trend = popularityAPIData.trendline;
        const popData = popularityAPIData.data;
        if (trend && trend.slope !== undefined && trend.intercept !== undefined && popData.length > 1) {
            const timestamps = popData.map(p => moment(p.releaseDate).valueOf());
            const minTimestamp = Math.min(...timestamps);
            const maxTimestamp = Math.max(...timestamps);
            const trendStartY = trend.slope * (minTimestamp / 1000) + trend.intercept;
            const trendEndY = trend.slope * (maxTimestamp / 1000) + trend.intercept;
            datasets.push({
                label: 'Trend',
                data: [ { x: minTimestamp, y: trendStartY }, { x: maxTimestamp, y: trendEndY } ],
                type: 'line', borderColor: 'rgba(108, 117, 125, 0.6)', 
                borderWidth: 2, borderDash: [6, 6], pointRadius: 0, fill: false, tension: 0, 
                yAxisID: 'yPrimary' 
            });
        }
        return datasets;
    }, [popularityAPIData, useSteppedLine]); // Added useSteppedLine dependency
    
    // --- Define annotations for Popularity Chart ---
    const popularityChartAnnotations = React.useMemo(() => {
        const annotations = {}; 
        if (showEvents) {
            const eventsData = [
                { 
                    date: '2024-08-16', 
                    eventName: 'Alien Romulus Release', 
                    color: 'rgba(220, 53, 69, 0.7)' 
                },
                // Add more events here by duplicating the object above
                // and changing the date, eventName, and optionally color.
                // Example:
                // { 
                //    date: 'YYYY-MM-DD', 
                //    eventName: 'Another Event Name', 
                //    color: 'rgba(100, 100, 255, 0.7)' // Blue example
                // },
            ];

            eventsData.forEach((event, index) => {
                annotations[`eventMarker_${index}`] = {
                    type: 'line',
                    scaleID: 'x', 
                    value: moment(event.date).valueOf(), 
                    borderColor: event.color.replace('0.7', '0.5'), 
                    borderWidth: 2,
                    borderDash: [4, 4], 
                    label: {
                        content: event.eventName, 
                        enabled: true, 
                        position: 'top', 
                        backgroundColor: event.color.replace('0.7', '0.5'), 
                        color: 'white',
                        font: { style: 'bold', size: 10 }, 
                        yAdjust: -5, 
                        rotation: 0 
                    }
                };
            });
        }
        return annotations; 
    }, [showEvents]); 
    
    const CHART_IDS = {
        SESSIONS_BAR: 'sessionsPerVersionChart', USERS_BAR: 'uniqueUsersPerVersionChart',
        TIMELINE: 'releaseTimelineChart', POPULARITY: 'popularityChart', HEATMAP: 'releaseHeatmap', 
    };

    // --- Rendering Logic ---
    return React.createElement('div', { className: 'container pb-5' }, 
        React.createElement('div', { className: 'dashboard-header' }, React.createElement('h1', null, 'OpenCAGE Analytics Dashboard')),

        // Rows 1 & 2: Stats Cards
        React.createElement('div', { className: 'row mb-4' }, /* ... Lifetime Stats ... */ 
             React.createElement('div', { className: 'col-md-12'},
                 React.createElement('div', {className: 'card'},
                     React.createElement('div', {className: 'card-header'}, 'Lifetime Stats'),
                     React.createElement('div', {className: 'card-body d-flex justify-content-around align-items-center flex-wrap'}, 
                         React.createElement(StatItem, {label: 'Total Unique Users', value: overallStats.totalUniqueUsers, isLoading: loading.overall, valueFontSize: '2rem'}),
                         React.createElement(StatItem, {label: 'Total Sessions', value: overallStats.totalSessions, isLoading: loading.overall, valueFontSize: '2rem'}),
                         React.createElement(StatItem, {label: 'Total Released Versions', value: overallStats.totalVersions, isLoading: loading.overall, valueFontSize: '2rem'})
                     )
                 )
             )
        ),
        React.createElement('div', { className: 'row mb-4' }, /* ... Latest Version Stats ... */ 
             React.createElement('div', { className: 'col-md-12'},
                 React.createElement('div', {className: 'card'},
                     React.createElement('div', {className: 'card-header'}, 
                         loading.latestVersion ? 'Latest Version Stats' : `Latest Version: ${latestVersionStats.versionNumber || 'N/A'}`
                     ),
                     React.createElement('div', {className: 'card-body d-flex justify-content-around align-items-center flex-wrap'},
                         React.createElement(StatItem, {
                             label: 'Released', 
                             value: latestVersionStats.releaseDate ? `${latestVersionStats.releaseDate} (${latestVersionStats.daysAgo} days ago)` : null, 
                             isLoading: loading.latestVersion
                         }),
                         React.createElement(StatItem, {label: 'Sessions', value: latestVersionStats.sessions, isLoading: loading.latestVersion}),
                         React.createElement(StatItem, {label: 'Unique Users', value: latestVersionStats.users, isLoading: loading.latestVersion})
                     )
                 )
             )
        ),

        // Row 3: Bar Charts
        React.createElement('div', { className: 'row' }, 
            (!expandedChartId || expandedChartId === CHART_IDS.SESSIONS_BAR) && React.createElement(ChartCard, {
                chartId: CHART_IDS.SESSIONS_BAR, title: 'Total Sessions per Version',
                isExpanded: expandedChartId === CHART_IDS.SESSIONS_BAR, onToggleExpand: handleToggleExpand,
                isLoading: loading.versionStats, error: error.versionStats
            }, React.createElement(BarChart, { chartId: CHART_IDS.SESSIONS_BAR, data: sessionsPerVersionChartData, label: 'Total Sessions', yAxisLabel: 'Number of Sessions', averageValue: statsPerVersionData.averageSessions })),
            
            (!expandedChartId || expandedChartId === CHART_IDS.USERS_BAR) && React.createElement(ChartCard, {
                chartId: CHART_IDS.USERS_BAR, title: 'Unique Users per Version',
                isExpanded: expandedChartId === CHART_IDS.USERS_BAR, onToggleExpand: handleToggleExpand,
                isLoading: loading.versionStats, error: error.versionStats
            }, React.createElement(BarChart, { chartId: CHART_IDS.USERS_BAR, data: uniqueUsersPerVersionChartData, label: 'Unique Users', yAxisLabel: 'Number of Unique Users', averageValue: statsPerVersionData.averageUniqueUsers }))
        ),

        // Row 4: Scatter Charts
        React.createElement('div', { className: 'row' }, 
            (!expandedChartId || expandedChartId === CHART_IDS.TIMELINE) && React.createElement(ChartCard, {
                chartId: CHART_IDS.TIMELINE, title: 'Version Release Timeline (Newest First)',
                isExpanded: expandedChartId === CHART_IDS.TIMELINE, onToggleExpand: handleToggleExpand,
                isLoading: loading.timeline, error: error.timeline
            }, React.createElement(ScatterChart, {
                            chartId: CHART_IDS.TIMELINE, datasets: releaseTimelineDatasets, 
                            xAxisLabel: 'Release Date', yAxisLabel: 'Version Sequence', xAxisType: 'time', 
                            yAxesConfig: releaseTimelineYAxesConfig, reverseXAxis: true 
                        })),

            (!expandedChartId || expandedChartId === CHART_IDS.POPULARITY) && React.createElement(ChartCard, {
                chartId: CHART_IDS.POPULARITY, title: 'Version Popularity Over Time (Newest First)',
                isExpanded: expandedChartId === CHART_IDS.POPULARITY, onToggleExpand: handleToggleExpand,
                isLoading: loading.popularity, error: error.popularity
            }, 
            React.createElement('div', { style: { position: 'relative', width: '100%', height: '100%' } }, 
                // Container for floating buttons - Moved to bottom-right
                React.createElement('div', { style: { position: 'absolute', bottom: '0px', left: '0px', zIndex: 10, display: 'flex', gap: '5px'} }, 
                    // Button for Stepped/Straight Line
                    React.createElement('button', {
                        type: 'button',
                        className: `btn btn-sm ${useSteppedLine ? 'btn-primary' : 'btn-outline-secondary'}`,
                        onClick: toggleSteppedLine,
                        title: useSteppedLine ? 'Use Straight Lines' : 'Use Stepped Lines'
                    }, useSteppedLine ? 'Stepped' : 'Lines'),
                    // Button for Events Marker
                    React.createElement('button', {
                        type: 'button',
                        className: `btn btn-sm ${showEvents ? 'btn-danger' : 'btn-outline-secondary'}`,
                        onClick: toggleShowEvents, 
                        title: showEvents ? 'Hide Events' : 'Show Events'
                    }, 'Events')
                ),
                 // Chart component - Pass annotations
                 React.createElement(ScatterChart, {
                    chartId: CHART_IDS.POPULARITY, datasets: popularityChartDatasets, 
                    xAxisLabel: 'Release Date', yAxisLabel: 'Popularity Score', xAxisType: 'time',
                    reverseXAxis: true,
                    customAnnotations: popularityChartAnnotations 
                })
            ))
        ),
        
        // Row 5: Heatmap 
        React.createElement('div', { className: 'row mb-4' }, /* ... Heatmap ... */ 
            React.createElement('div', { className: 'col-md-12' },
                React.createElement('div', { className: 'card' },
                    React.createElement('div', { className: 'card-header' }, 'Release Frequency Heatmap (Newest First)'),
                    React.createElement('div', { className: 'card-body p-2' }, 
                        loading.heatmap ? React.createElement(LoadingSpinner) : error.heatmap ? React.createElement(ErrorMessage, { message: error.heatmap }) :
                        React.createElement(HeatmapTimeline, { data: releaseHeatmapData }) 
                    )
                )
            )
        ),

        // Row 6: User History 
        React.createElement('div', { className: 'row mb-4' }, /* ... User History ... */ 
            React.createElement('div', { className: 'col-md-12' }, 
                React.createElement('div', { className: 'card' },
                    React.createElement('div', { className: 'card-header' }, 'User Session History'),
                    React.createElement('div', { className: 'card-body' },
                        loading.clientIds ? React.createElement(LoadingSpinner) : error.clientIds ? React.createElement(ErrorMessage, { message: error.clientIds }) :
                        React.createElement('div', { className: 'form-group' },
                            React.createElement('label', { htmlFor: 'clientSelect' }, 'Select Client ID:'),
                            React.createElement('select', { id: 'clientSelect', className: 'form-control form-control-sm', value: selectedClientId, onChange: handleClientSelect }, 
                                React.createElement('option', { value: '' }, 'Select a Client ID...'),
                                allClientData.map(client => React.createElement('option', { 
                                    key: client.client_id, value: client.client_id 
                                }, `${client.client_id} (${client.total_sessions} sessions)`)) 
                            )
                        ),
                        loading.userHistory ? React.createElement(LoadingSpinner) : 
                        error.userHistory ? React.createElement(ErrorMessage, { message: error.userHistory }) :
                        userHistory ? (
                            userHistory.length > 0 ?
                            React.createElement('div', { id: 'userHistoryContainer', style:{maxHeight: '300px', overflowY: 'auto'} }, 
                                React.createElement('h6', {className: 'mt-2'}, `History for Client: ${selectedClientId}`), 
                                React.createElement('ul', { className: 'list-group list-group-flush mt-1' },
                                    userHistory.map(item => React.createElement('li', { 
                                            key: item.version + '-' + item.sessions, 
                                            className: 'list-group-item d-flex justify-content-between align-items-center py-2 px-0' 
                                        }, 
                                        React.createElement('span', { 
                                            title: item.releaseDate ? `Released: ${moment(item.releaseDate).format('Do MMM YYYY')}` : 'Release date not available' 
                                        }, `Version: ${item.version}`),
                                        React.createElement('span', { className: 'badge badge-primary badge-pill' }, `${item.sessions} sessions`)
                                    ))
                                )
                            ) : React.createElement('p', {className: 'text-muted mt-3'}, 'No session history found for this user.')
                        ) : (selectedClientId ? React.createElement('p', {className: 'text-muted mt-3'}, 'Loading history...') : React.createElement('p', {className: 'text-muted mt-3'}, 'Select a client.'))
                    )
                )
            )
        ),
        // Row 7: Top Users
        React.createElement('div', { className: 'row' }, /* ... Top Users ... */ 
            React.createElement('div', { className: 'col-md-12' },
                React.createElement('div', { className: 'card' },
                    React.createElement('div', { className: 'card-header' }, 'Top 10 Users by Sessions'),
                    React.createElement('div', { className: 'card-body', style:{maxHeight: '428px', overflowY: 'auto'} }, 
                        loading.topUsers ? React.createElement(LoadingSpinner) : error.topUsers ? React.createElement(ErrorMessage, { message: error.topUsers }) :
                        React.createElement(TopUsersList, { data: topUsersData })
                    )
                )
            )
        )
    );
}

ReactDOM.render(React.createElement(App), document.getElementById('root'));
