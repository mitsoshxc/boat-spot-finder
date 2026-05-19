(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var container = document.getElementById('canvas-container');
        if (!container) return;

        var marinaId = container.dataset.marinaId;
        var spotCreateUrl = container.dataset.spotCreateUrl;
        var stageWidth = container.clientWidth;
        var stageHeight = container.clientHeight;
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        var token = tokenInput ? tokenInput.value : '';

        var stage = new Konva.Stage({
            container: 'canvas-container',
            width: stageWidth,
            height: stageHeight
        });

        var layer = new Konva.Layer();
        stage.add(layer);

        var transformer = new Konva.Transformer({
            enabledAnchors: ['top-left', 'top-right', 'bottom-left', 'bottom-right'],
            rotateEnabled: true
        });
        layer.add(transformer);

        var spotsById = new Map();
        var unplacedCount = 0;

        function addSpotToCanvas(spot) {
            var placed = spot.canvasX != null && spot.canvasY != null && spot.canvasW != null && spot.canvasH != null;
            var x, y, w, h, rotation, dashed, fill, stroke, opacity;

            if (placed) {
                x = spot.canvasX;
                y = spot.canvasY;
                w = spot.canvasW;
                h = spot.canvasH;
                rotation = spot.canvasRotation != null ? spot.canvasRotation : 0;
                dashed = false;
                if (spot.isActive) {
                    fill = '#4a90e2';
                    stroke = '#1f4e96';
                    opacity = 0.55;
                } else {
                    fill = '#9a9a9a';
                    stroke = '#5a5a5a';
                    opacity = 0.4;
                }
            } else {
                x = 20 + (unplacedCount * 12);
                y = 20 + (unplacedCount * 12);
                w = 80;
                h = 50;
                rotation = 0;
                dashed = true;
                fill = 'transparent';
                stroke = '#1f4e96';
                opacity = 1;
                unplacedCount++;
            }

            var rect = new Konva.Rect({
                x: x,
                y: y,
                width: w,
                height: h,
                rotation: rotation,
                fill: fill,
                stroke: stroke,
                strokeWidth: 1.5,
                opacity: opacity,
                draggable: true
            });

            if (dashed) {
                rect.dash([6, 4]);
            }

            var label = new Konva.Text({
                text: spot.name,
                fontSize: 11,
                fontFamily: 'Manrope, sans-serif',
                fill: dashed ? '#1f4e96' : '#ffffff',
                listening: false
            });

            function positionLabel() {
                var rw = rect.width() * rect.scaleX();
                var rh = rect.height() * rect.scaleY();
                label.x(rect.x() - rw / 2 + rect.width() / 2 - label.width() / 2);
                label.y(rect.y() - rh / 2 + rect.height() / 2 - label.height() / 2);
                label.rotation(rect.rotation());
                label.offsetX(label.width() / 2);
                label.offsetY(label.height() / 2);
                label.x(rect.x() + rect.width() / 2);
                label.y(rect.y() + rect.height() / 2);
            }

            function updateLabelPosition() {
                label.x(rect.x() + (rect.width() * rect.scaleX()) / 2);
                label.y(rect.y() + (rect.height() * rect.scaleY()) / 2);
                label.offsetX(label.width() / 2);
                label.offsetY(label.height() / 2);
                label.rotation(rect.rotation());
            }

            updateLabelPosition();

            rect.on('dragmove', function () {
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('transform', function () {
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('click tap', function () {
                transformer.nodes([rect]);
                layer.batchDraw();
            });

            layer.add(rect);
            layer.add(label);

            spotsById.set(spot.id, { node: rect, label: label, name: spot.name, isActive: spot.isActive });
        }

        stage.on('click tap', function (e) {
            if (e.target === stage) {
                transformer.nodes([]);
                layer.batchDraw();
            }
        });

        fetch('/browse/marina/' + marinaId + '/layout-data')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.backgroundImagePath) {
                    var img = new Image();
                    img.onload = function () {
                        var bg = new Konva.Image({
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
                    var bg = new Konva.Rect({
                        x: 0,
                        y: 0,
                        width: data.layoutWidth,
                        height: data.layoutHeight,
                        fill: '#e0e0e0',
                        listening: false
                    });
                    layer.add(bg);
                    bg.moveToBottom();
                    layer.batchDraw();
                }

                if (data.spots) {
                    data.spots.forEach(function (spot) {
                        addSpotToCanvas(spot);
                    });
                }

                layer.batchDraw();
            })
            .catch(function (err) {
                console.error('Failed to load layout data', err);
            });

        var modal = document.getElementById('add-spot-modal');
        var addSpotForm = document.getElementById('add-spot-form');
        var modalErrors = modal ? modal.querySelector('.modal__errors') : null;

        function openModal() {
            if (modal) modal.removeAttribute('hidden');
        }

        function closeModal() {
            if (modal) modal.setAttribute('hidden', '');
            if (addSpotForm) addSpotForm.reset();
            if (modalErrors) {
                modalErrors.setAttribute('hidden', '');
                modalErrors.innerHTML = '';
            }
        }

        var btnAddSpot = document.getElementById('btn-add-spot');
        if (btnAddSpot) {
            btnAddSpot.addEventListener('click', openModal);
        }

        if (modal) {
            modal.addEventListener('click', function (e) {
                if (e.target.hasAttribute('data-modal-dismiss')) {
                    closeModal();
                }
            });
        }

        if (addSpotForm) {
            addSpotForm.addEventListener('submit', function (e) {
                e.preventDefault();

                var formData = new FormData(addSpotForm);
                var payload = {
                    Name: formData.get('Name'),
                    Description: formData.get('Description'),
                    LengthMeters: parseFloat(formData.get('LengthMeters')),
                    WidthMeters: parseFloat(formData.get('WidthMeters')),
                    DepthMeters: parseFloat(formData.get('DepthMeters')),
                    PricePerDay: parseFloat(formData.get('PricePerDay'))
                };

                fetch(spotCreateUrl, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(payload)
                })
                    .then(function (r) {
                        if (r.ok) {
                            return r.json().then(function (data) {
                                addSpotToCanvas({
                                    id: data.id,
                                    name: data.name,
                                    canvasX: null,
                                    canvasY: null,
                                    canvasW: null,
                                    canvasH: null,
                                    canvasRotation: null,
                                    isActive: false
                                });
                                layer.batchDraw();
                                closeModal();
                            });
                        } else {
                            return r.json().then(function (errors) {
                                var messages = [];
                                if (errors && typeof errors === 'object') {
                                    Object.keys(errors).forEach(function (key) {
                                        var errs = errors[key];
                                        if (Array.isArray(errs)) {
                                            errs.forEach(function (msg) { messages.push(msg); });
                                        } else if (typeof errs === 'string') {
                                            messages.push(errs);
                                        }
                                    });
                                }
                                if (messages.length === 0) messages.push('An error occurred. Please try again.');
                                showModalErrors(messages);
                            }).catch(function () {
                                showModalErrors(['An error occurred. Please try again.']);
                            });
                        }
                    })
                    .catch(function () {
                        showModalErrors(['Network error. Please check your connection and try again.']);
                    });
            });
        }

        function showModalErrors(messages) {
            if (!modalErrors) return;
            modalErrors.removeAttribute('hidden');
            modalErrors.innerHTML = messages.map(function (m) {
                return '<p>' + escapeHtml(m) + '</p>';
            }).join('');
        }

        function escapeHtml(str) {
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }

        var btnSaveLayout = document.getElementById('btn-save-layout');
        if (btnSaveLayout) {
            btnSaveLayout.addEventListener('click', function () {
                var positions = [];
                spotsById.forEach(function (entry, id) {
                    var node = entry.node;
                    var newW = node.width() * node.scaleX();
                    var newH = node.height() * node.scaleY();
                    node.scaleX(1);
                    node.scaleY(1);
                    node.width(newW);
                    node.height(newH);

                    positions.push({
                        Id: id,
                        CanvasX: node.x(),
                        CanvasY: node.y(),
                        CanvasW: newW,
                        CanvasH: newH,
                        CanvasRotation: node.rotation()
                    });
                });

                fetch('/placeowner/marinas/' + marinaId + '/spots/save-positions', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(positions)
                })
                    .then(function (r) {
                        if (r.ok) {
                            var btnText = btnSaveLayout.textContent;
                            btnSaveLayout.textContent = 'Saved ✓';
                            setTimeout(function () {
                                btnSaveLayout.textContent = btnText;
                            }, 2000);
                        } else {
                            r.text().then(function (body) {
                                console.error('Save positions failed', body);
                            });
                        }
                    })
                    .catch(function (err) {
                        console.error('Save positions network error', err);
                    });
            });
        }
    });
}());
