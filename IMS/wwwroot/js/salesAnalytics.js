window.loadTopProductsChart = function (names, quantities) {
    const ctx = document.getElementById("topProductsChart");

    if (!ctx) {
        console.error("Chart canvas not found");
        return;
    }

    new Chart(ctx, {
        type: "bar",
        data: {
            labels: names,
            datasets: [{
                label: "Top Selling Products",
                data: quantities,
                borderWidth: 1
            }]
        }
    });
};
