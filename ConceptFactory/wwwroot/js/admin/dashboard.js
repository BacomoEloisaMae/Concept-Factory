// wwwroot/js/admin/dashboard.js
// Logic for the Admin Dashboard (Views/Home/pIndex.cshtml). Server-rendered
// data arrives via window.CF_DASHBOARD (see the inline <script> in pIndex
// right before this file loads) — this file only reads that data, it
// never inlines any server values itself.

(function () {
    // Chart.js needs its canvas inside a container with a real, fixed
    // height — .dash-donut-wrap / .dash-revenue-chart-wrap give it one
    // (see dashboard.css) — and maintainAspectRatio:false so it fills
    // that box instead of trying to auto-size a flex/grid parent, which
    // is what silently produced a blank/0-height chart before.
    if (typeof Chart === 'undefined') {
        console.error('Chart.js failed to load — Production Overview / Revenue Overview charts will not render.');
        return;
    }

    const data = window.CF_DASHBOARD || {};

    const donutEl = document.getElementById('productionDonut');
    if (donutEl) {
        new Chart(donutEl, {
            type: 'doughnut',
            data: {
                labels: data.stageLabels,
                datasets: [{ data: data.stageData, backgroundColor: data.stageColors, borderWidth: 0 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '72%',
                plugins: { legend: { display: false }, tooltip: { enabled: true } }
            }
        });
    }

    const weekLabels = ['Week 1', 'Week 2', 'Week 3', 'Week 4', 'Week 5'];
    const barEl = document.getElementById('revenueBar');
    if (barEl) {
        new Chart(barEl, {
            type: 'bar',
            data: {
                labels: weekLabels,
                datasets: [{ data: data.weeklyRevenue, backgroundColor: '#5B5EF4', borderRadius: 6, maxBarThickness: 40 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true, ticks: { callback: v => '₱' + v.toLocaleString() } } }
            }
        });
    }
})();
