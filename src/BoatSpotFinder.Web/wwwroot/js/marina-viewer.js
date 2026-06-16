(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const container = document.getElementById('canvas-container');
        if (!container) { return; }

        const marinaId = container.dataset.marinaId;
        const spotStatusesUrl = container.dataset.spotStatusesUrl;
        const bookable = container.dataset.bookable === 'true';

        const stage = new Konva.Stage({
            container: 'canvas-container',
            width: container.clientWidth,
            height: container.clientHeight
        });
        const bgLayer = new Konva.Layer();
        const spotLayer = new Konva.Layer();
        const tooltipLayer = new Konva.Layer();
        stage.add(bgLayer);
        stage.add(spotLayer);
        stage.add(tooltipLayer);

        const STATUS_COLORS = {
            Free: { fill: 'rgba(46, 107, 79, 0.55)', stroke: '#2E6B4F' },
            Booked: { fill: 'rgba(176, 141, 87, 0.6)', stroke: '#8E6F3F' },
            Unavailable: { fill: 'rgba(107, 118, 132, 0.5)', stroke: '#4A535F' },
            Incompatible: { fill: 'rgba(201, 121, 46, 0.62)', stroke: '#9E5A1E' }
        };

        const tooltip = new Konva.Label({ visible: false, listening: false });
        tooltip.add(new Konva.Tag({ fill: 'rgba(11, 31, 53, 0.92)', cornerRadius: 4 }));
        const tooltipText = new Konva.Text({
            text: '',
            fontFamily: 'Manrope, sans-serif',
            fontSize: 12,
            lineHeight: 1.35,
            padding: 8,
            fill: '#ffffff'
        });
        tooltip.add(tooltipText);
        tooltipLayer.add(tooltip);

        function drawBackground(data) {
            if (data.backgroundImagePath) {
                const img = new Image();
                img.onload = function () {
                    const bg = new Konva.Image({
                        image: img,
                        x: 0,
                        y: 0,
                        width: data.layoutWidth,
                        height: data.layoutHeight,
                        listening: false
                    });
                    bgLayer.add(bg);
                    bg.moveToBottom();
                    bgLayer.batchDraw();
                };
                img.src = data.backgroundImagePath;
            } else {
                const bg = new Konva.Rect({
                    x: 0,
                    y: 0,
                    width: data.layoutWidth,
                    height: data.layoutHeight,
                    fill: '#e0e0e0',
                    listening: false
                });
                bgLayer.add(bg);
                bg.moveToBottom();
                bgLayer.batchDraw();
            }
        }

        function statusUrlWithParams() {
            const params = new URLSearchParams();
            const vesselSel = document.getElementById('vessel-select');
            const startEl = document.getElementById('start-date');
            const endEl = document.getElementById('end-date');
            if (vesselSel && vesselSel.value) { params.append('vesselId', vesselSel.value); }
            if (startEl && startEl.value) { params.append('start', startEl.value); }
            if (endEl && endEl.value) { params.append('end', endEl.value); }
            const qs = params.toString();
            return qs ? spotStatusesUrl + '?' + qs : spotStatusesUrl;
        }

        function bookingUrl(spot) {
            const params = new URLSearchParams();
            params.append('spotId', spot.id);
            const startEl = document.getElementById('start-date');
            const endEl = document.getElementById('end-date');
            const vesselSel = document.getElementById('vessel-select');
            if (startEl && startEl.value) { params.append('startDate', startEl.value); }
            if (endEl && endEl.value) { params.append('endDate', endEl.value); }
            if (vesselSel && vesselSel.value) { params.append('vesselId', vesselSel.value); }
            return '/bookings/create?' + params.toString();
        }

        function showTooltip(spot) {
            const lines = [spot.name];
            if (spot.pricePerDay != null) {
                lines.push('€' + Number(spot.pricePerDay).toFixed(2) + ' / day');
            }
            lines.push(spot.spotLengthMeters + ' × ' + spot.spotWidthMeters + ' × ' + spot.spotDepthMeters + ' m');
            tooltipText.text(lines.join('\n'));
            const pos = stage.getPointerPosition();
            tooltip.position({ x: pos.x + 12, y: pos.y + 12 });
            tooltip.visible(true);
            tooltipLayer.batchDraw();
        }

        function hideTooltip() {
            tooltip.visible(false);
            tooltipLayer.batchDraw();
        }

        function drawSpots(spots) {
            spotLayer.destroyChildren();
            spots.forEach(function (spot) {
                const placed = spot.canvasX != null && spot.canvasY != null &&
                               spot.canvasW != null && spot.canvasH != null;
                if (!placed) { return; }

                const colors = STATUS_COLORS[spot.status] || STATUS_COLORS.Unavailable;
                const rotation = spot.rotation != null ? spot.rotation : 0;
                const clickable = bookable && spot.status === 'Free';

                const rect = new Konva.Rect({
                    x: spot.canvasX,
                    y: spot.canvasY,
                    width: spot.canvasW,
                    height: spot.canvasH,
                    rotation: rotation,
                    fill: colors.fill,
                    stroke: colors.stroke,
                    strokeWidth: 1.5,
                    listening: true
                });

                const label = new Konva.Text({
                    text: spot.name,
                    fontSize: 11,
                    fontFamily: 'Manrope, sans-serif',
                    fill: '#ffffff',
                    listening: false
                });
                label.offsetX(label.width() / 2);
                label.offsetY(label.height() / 2);
                label.x(spot.canvasX + spot.canvasW / 2);
                label.y(spot.canvasY + spot.canvasH / 2);
                label.rotation(rotation);

                rect.on('mouseover', function () {
                    showTooltip(spot);
                    if (clickable) { container.style.cursor = 'pointer'; }
                });
                rect.on('mousemove', function () {
                    const pos = stage.getPointerPosition();
                    tooltip.position({ x: pos.x + 12, y: pos.y + 12 });
                    tooltipLayer.batchDraw();
                });
                rect.on('mouseout', function () {
                    hideTooltip();
                    container.style.cursor = 'default';
                });

                if (clickable) {
                    rect.on('click tap', function () {
                        window.location.href = bookingUrl(spot);
                    });
                }

                spotLayer.add(rect);
                spotLayer.add(label);
            });
            spotLayer.batchDraw();
        }

        function refreshStatuses() {
            fetch(statusUrlWithParams())
                .then(function (r) { return r.json(); })
                .then(function (spots) { drawSpots(spots); })
                .catch(function (err) { console.error('Failed to load statuses', err); });
        }

        fetch('/browse/marina/' + marinaId + '/layout-data')
            .then(function (r) { return r.json(); })
            .then(function (layout) {
                drawBackground(layout);
                refreshStatuses();
            })
            .catch(function (err) { console.error('Failed to load layout', err); });

        const vesselSelect = document.getElementById('vessel-select');
        if (vesselSelect) {
            vesselSelect.addEventListener('change', refreshStatuses);
        }
        const applyButton = document.getElementById('apply-filter');
        if (applyButton) {
            applyButton.addEventListener('click', refreshStatuses);
        }
    });
}());
