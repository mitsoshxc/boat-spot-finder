(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const container = document.getElementById('canvas-container');
        if (!container) { return; }

        const marinaId = container.dataset.marinaId;
        const spotStatusesUrl = container.dataset.spotStatusesUrl;
        const stageWidth = container.clientWidth;
        const stageHeight = container.clientHeight;

        const stage = new Konva.Stage({
            container: 'canvas-container',
            width: stageWidth,
            height: stageHeight
        });
        const layer = new Konva.Layer();
        stage.add(layer);

        const STATUS_COLORS = {
            Free: { fill: 'rgba(46, 107, 79, 0.55)', stroke: '#2E6B4F' },
            Booked: { fill: 'rgba(176, 141, 87, 0.6)', stroke: '#8E6F3F' },
            Unavailable: { fill: 'rgba(107, 118, 132, 0.5)', stroke: '#4A535F' }
        };

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
                    layer.add(bg);
                    bg.moveToBottom();
                    layer.batchDraw();
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
                layer.add(bg);
                bg.moveToBottom();
            }
        }

        function drawSpot(spot, status) {
            const colors = STATUS_COLORS[status] || STATUS_COLORS.Unavailable;
            const rotation = spot.canvasRotation != null ? spot.canvasRotation : 0;

            const rect = new Konva.Rect({
                x: spot.canvasX,
                y: spot.canvasY,
                width: spot.canvasW,
                height: spot.canvasH,
                rotation: rotation,
                fill: colors.fill,
                stroke: colors.stroke,
                strokeWidth: 1.5,
                listening: false
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

            layer.add(rect);
            layer.add(label);
        }

        Promise.all([
            fetch('/browse/marina/' + marinaId + '/layout-data').then(function (r) { return r.json(); }),
            fetch(spotStatusesUrl).then(function (r) { return r.json(); })
        ])
            .then(function (results) {
                const layout = results[0];
                const statuses = results[1];

                const statusById = new Map();
                statuses.forEach(function (s) { statusById.set(s.id, s.status); });

                drawBackground(layout);

                (layout.spots || []).forEach(function (spot) {
                    const placed = spot.canvasX != null && spot.canvasY != null &&
                                   spot.canvasW != null && spot.canvasH != null;
                    if (!placed) { return; }
                    const status = statusById.get(spot.id) || (spot.isActive ? 'Free' : 'Unavailable');
                    drawSpot(spot, status);
                });

                layer.batchDraw();
            })
            .catch(function (err) {
                console.error('Failed to load layout', err);
            });
    });
}());
