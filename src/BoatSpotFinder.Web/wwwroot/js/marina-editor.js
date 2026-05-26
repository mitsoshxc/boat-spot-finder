(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var uploadForm = document.getElementById('background-upload-form');
        var uploadInput = uploadForm ? uploadForm.querySelector('input[name="backgroundImage"]') : null;
        var uploadError = document.getElementById('background-upload-error');

        if (uploadForm && uploadInput && uploadError) {
            uploadInput.addEventListener('change', function () {
                uploadError.textContent = '';
                uploadError.hidden = true;
            });

            uploadForm.addEventListener('submit', function (e) {
                var file = uploadInput.files && uploadInput.files[0];
                if (!file) { return; }

                var allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
                var allowedExts = ['.jpg', '.jpeg', '.png', '.webp'];
                var maxSize = 5 * 1024 * 1024;
                var errorMsg = '';

                if (allowedTypes.indexOf(file.type) === -1) {
                    errorMsg = 'Please choose a JPG, PNG, or WebP image.';
                } else {
                    var nameLower = file.name.toLowerCase();
                    var extOk = false;
                    for (var i = 0; i < allowedExts.length; i++) {
                        if (nameLower.slice(-allowedExts[i].length) === allowedExts[i]) {
                            extOk = true;
                            break;
                        }
                    }
                    if (!extOk) {
                        errorMsg = 'File extension must be .jpg, .jpeg, .png, or .webp.';
                    } else if (file.size > maxSize) {
                        errorMsg = 'File size must be 5 MB or less.';
                    }
                }

                if (errorMsg) {
                    e.preventDefault();
                    uploadError.textContent = errorMsg;
                    uploadError.hidden = false;
                }
            });
        }

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
            enabledAnchors: [
                'top-left', 'top-right', 'bottom-left', 'bottom-right',
                'middle-left', 'middle-right', 'top-center', 'bottom-center'
            ],
            rotateEnabled: false,
            boundBoxFunc: function (oldBox, newBox) {
                return resizeBoundBox(oldBox, newBox);
            }
        });
        layer.add(transformer);

        var spotsById = new Map();
        var unplacedCount = 0;
        var layoutBounds = { w: 0, h: 0 };

        var SNAP_THRESHOLD = 8;

        function getOtherRects(draggedNode) {
            var rects = [];
            spotsById.forEach(function (entry) {
                if (entry.node !== draggedNode) {
                    rects.push(entry.node.getClientRect({ relativeTo: layer }));
                }
            });
            return rects;
        }

        function rectsOverlap(a, b) {
            return !(a.x + a.width <= b.x || b.x + b.width <= a.x ||
                     a.y + a.height <= b.y || b.y + b.height <= a.y);
        }

        function isOutOfBounds(r) {
            return r.x < 0 || r.y < 0 ||
                   r.x + r.width > layoutBounds.w ||
                   r.y + r.height > layoutBounds.h;
        }

        function findEmptySlot(w, h) {
            if (layoutBounds.w <= 0 || layoutBounds.h <= 0) {
                return null;
            }

            var existing = [];
            spotsById.forEach(function (entry) {
                existing.push(entry.node.getClientRect({ relativeTo: layer }));
            });

            var step = 20;
            for (var y = 20; y + h <= layoutBounds.h - 20; y += step) {
                for (var x = 20; x + w <= layoutBounds.w - 20; x += step) {
                    var candidate = { x: x, y: y, width: w, height: h };
                    var clash = false;
                    for (var i = 0; i < existing.length; i++) {
                        if (rectsOverlap(candidate, existing[i])) {
                            clash = true;
                            break;
                        }
                    }
                    if (!clash) {
                        return { x: x, y: y };
                    }
                }
            }
            return null;
        }

        function applySnapDuringDrag(node) {
            var aabb = node.getClientRect({ relativeTo: layer });
            var others = getOtherRects(node);

            var bestDx = null;
            var bestDy = null;

            function trySnapX(delta) {
                if (Math.abs(delta) <= SNAP_THRESHOLD) {
                    if (bestDx === null || Math.abs(delta) < Math.abs(bestDx)) {
                        bestDx = delta;
                    }
                }
            }

            function trySnapY(delta) {
                if (Math.abs(delta) <= SNAP_THRESHOLD) {
                    if (bestDy === null || Math.abs(delta) < Math.abs(bestDy)) {
                        bestDy = delta;
                    }
                }
            }

            var myLeft = aabb.x;
            var myRight = aabb.x + aabb.width;
            var myTop = aabb.y;
            var myBottom = aabb.y + aabb.height;

            trySnapX(0 - myLeft);
            trySnapX(layoutBounds.w - myRight);
            trySnapY(0 - myTop);
            trySnapY(layoutBounds.h - myBottom);

            for (var i = 0; i < others.length; i++) {
                var o = others[i];
                var oLeft = o.x;
                var oRight = o.x + o.width;
                var oTop = o.y;
                var oBottom = o.y + o.height;

                trySnapX(oRight - myLeft);
                trySnapX(oLeft - myRight);
                trySnapY(oBottom - myTop);
                trySnapY(oTop - myBottom);
            }

            if (bestDx !== null) {
                node.x(node.x() + bestDx);
            }
            if (bestDy !== null) {
                node.y(node.y() + bestDy);
            }
        }

        function resizeBoundBox(oldBox, newBox) {
            if (layoutBounds.w <= 0 || layoutBounds.h <= 0) return newBox;
            if (!transformer || transformer.nodes().length === 0) return newBox;
            var draggedNode = transformer.nodes()[0];
            var others = getOtherRects(draggedNode);

            var oldLeft = oldBox.x;
            var oldRight = oldBox.x + oldBox.width;
            var oldTop = oldBox.y;
            var oldBottom = oldBox.y + oldBox.height;
            var newLeft = newBox.x;
            var newRight = newBox.x + newBox.width;
            var newTop = newBox.y;
            var newBottom = newBox.y + newBox.height;

            var leftChanged = Math.abs(newLeft - oldLeft) > 0.5;
            var rightChanged = Math.abs(newRight - oldRight) > 0.5;
            var topChanged = Math.abs(newTop - oldTop) > 0.5;
            var bottomChanged = Math.abs(newBottom - oldBottom) > 0.5;

            function pickBestSnap(value, targets) {
                var best = null;
                for (var i = 0; i < targets.length; i++) {
                    var d = targets[i] - value;
                    if (Math.abs(d) < SNAP_THRESHOLD && (best === null || Math.abs(d) < Math.abs(best))) {
                        best = d;
                    }
                }
                return best;
            }

            if (rightChanged) {
                var rightTargets = [layoutBounds.w];
                for (var i = 0; i < others.length; i++) rightTargets.push(others[i].x);
                var d = pickBestSnap(newRight, rightTargets);
                if (d !== null) newRight = newRight + d;
            }
            if (leftChanged) {
                var leftTargets = [0];
                for (var j = 0; j < others.length; j++) leftTargets.push(others[j].x + others[j].width);
                var d2 = pickBestSnap(newLeft, leftTargets);
                if (d2 !== null) newLeft = newLeft + d2;
            }
            if (bottomChanged) {
                var bottomTargets = [layoutBounds.h];
                for (var k = 0; k < others.length; k++) bottomTargets.push(others[k].y);
                var d3 = pickBestSnap(newBottom, bottomTargets);
                if (d3 !== null) newBottom = newBottom + d3;
            }
            if (topChanged) {
                var topTargets = [0];
                for (var m = 0; m < others.length; m++) topTargets.push(others[m].y + others[m].height);
                var d4 = pickBestSnap(newTop, topTargets);
                if (d4 !== null) newTop = newTop + d4;
            }

            var snapped = {
                x: newLeft,
                y: newTop,
                width: newRight - newLeft,
                height: newBottom - newTop,
                rotation: newBox.rotation
            };

            if (snapped.width <= 0 || snapped.height <= 0) return oldBox;
            if (snapped.x < 0 || snapped.y < 0 ||
                snapped.x + snapped.width > layoutBounds.w ||
                snapped.y + snapped.height > layoutBounds.h) {
                return oldBox;
            }
            for (var n = 0; n < others.length; n++) {
                if (rectsOverlap(snapped, others[n])) {
                    return oldBox;
                }
            }

            return snapped;
        }

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
                    fill = '#6B7684';
                    stroke = '#3C4654';
                    opacity = 0.55;
                } else {
                    fill = '#9a9a9a';
                    stroke = '#5a5a5a';
                    opacity = 0.4;
                }
            } else {
                w = 80;
                h = 50;
                var slot = findEmptySlot(w, h);
                if (slot) {
                    x = slot.x;
                    y = slot.y;
                } else {
                    x = 20 + (unplacedCount * 12);
                    y = 20 + (unplacedCount * 12);
                }
                rotation = 0;
                dashed = true;
                fill = '#6B7684';
                stroke = '#3C4654';
                opacity = 0.55;
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
                fill: '#ffffff',
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

            rect.on('dragstart', function () {
                rect._preDragX = rect.x();
                rect._preDragY = rect.y();
            });

            rect.on('dragmove', function () {
                applySnapDuringDrag(rect);
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('dragend', function () {
                var aabb = rect.getClientRect({ relativeTo: layer });
                var others = getOtherRects(rect);
                var bad = isOutOfBounds(aabb);
                if (!bad) {
                    for (var i = 0; i < others.length; i++) {
                        if (rectsOverlap(aabb, others[i])) {
                            bad = true;
                            break;
                        }
                    }
                }
                if (bad) {
                    rect.x(rect._preDragX);
                    rect.y(rect._preDragY);
                    updateLabelPosition();
                    layer.batchDraw();
                }
            });

            rect.on('transformstart', function () {
                rect._preTransformX = rect.x();
                rect._preTransformY = rect.y();
                rect._preTransformW = rect.width();
                rect._preTransformH = rect.height();
                rect._preTransformScaleX = rect.scaleX();
                rect._preTransformScaleY = rect.scaleY();
                rect._preTransformRotation = rect.rotation();
            });

            rect.on('transform', function () {
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('transformend', function () {
                var aabb = rect.getClientRect({ relativeTo: layer });
                var others = getOtherRects(rect);
                var bad = isOutOfBounds(aabb);
                if (!bad) {
                    for (var i = 0; i < others.length; i++) {
                        if (rectsOverlap(aabb, others[i])) {
                            bad = true;
                            break;
                        }
                    }
                }
                if (bad) {
                    rect.x(rect._preTransformX);
                    rect.y(rect._preTransformY);
                    rect.width(rect._preTransformW);
                    rect.height(rect._preTransformH);
                    rect.scaleX(rect._preTransformScaleX);
                    rect.scaleY(rect._preTransformScaleY);
                    rect.rotation(rect._preTransformRotation);
                    updateLabelPosition();
                    layer.batchDraw();
                }
            });

            rect.on('click tap', function () {
                transformer.nodes([rect]);
                layer.batchDraw();
            });

            layer.add(rect);
            layer.add(label);
            updateLabelPosition();

            spotsById.set(spot.id, { node: rect, label: label, name: spot.name, isActive: spot.isActive });
        }

        function appendSpotToSidebar(spot) {
            var sidebar = document.getElementById('spot-sidebar');
            if (!sidebar) return;

            var countEl = sidebar.querySelector('.spot-sidebar__count');
            if (countEl) {
                var current = parseInt(countEl.textContent, 10) || 0;
                countEl.textContent = (current + 1).toString();
            }

            var emptyEl = sidebar.querySelector('.spot-sidebar__empty');
            if (emptyEl) {
                emptyEl.parentNode.removeChild(emptyEl);
            }

            var list = sidebar.querySelector('.spot-sidebar__list');
            if (!list) {
                list = document.createElement('ul');
                list.className = 'spot-sidebar__list';
                sidebar.appendChild(list);
            }

            var editUrl = container.dataset.spotEditUrlTemplate
                ? container.dataset.spotEditUrlTemplate.replace('__ID__', spot.id)
                : '#';

            var li = document.createElement('li');
            li.className = 'spot-sidebar__item';
            li.dataset.spotId = spot.id;

            var row = document.createElement('div');
            row.className = 'spot-sidebar__row';

            var nameSpan = document.createElement('span');
            nameSpan.className = 'spot-sidebar__name';
            nameSpan.textContent = spot.name;
            row.appendChild(nameSpan);

            var pill = document.createElement('span');
            pill.className = 'pill pill--unplaced';
            var dot = document.createElement('span');
            dot.className = 'pill__dot';
            dot.setAttribute('aria-hidden', 'true');
            pill.appendChild(dot);
            pill.appendChild(document.createTextNode('Unplaced'));
            row.appendChild(pill);

            li.appendChild(row);

            var actions = document.createElement('div');
            actions.className = 'spot-sidebar__actions';

            var editLink = document.createElement('a');
            editLink.className = 'spot-sidebar__edit';
            editLink.href = editUrl;
            editLink.textContent = 'Edit details →';
            actions.appendChild(editLink);

            var deleteBtn = document.createElement('button');
            deleteBtn.type = 'button';
            deleteBtn.className = 'spot-sidebar__delete';
            deleteBtn.dataset.spotId = spot.id;
            deleteBtn.dataset.spotName = spot.name;
            deleteBtn.textContent = 'Delete';
            actions.appendChild(deleteBtn);

            li.appendChild(actions);

            list.appendChild(li);
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
                layoutBounds.w = data.layoutWidth || 1200;
                layoutBounds.h = data.layoutHeight || 800;

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

        function clearModalFieldErrors() {
            if (!addSpotForm) return;
            var inputs = addSpotForm.querySelectorAll('[aria-invalid]');
            for (var i = 0; i < inputs.length; i++) {
                inputs[i].removeAttribute('aria-invalid');
            }
            var fieldErrors = addSpotForm.querySelectorAll('.field__error');
            for (var j = 0; j < fieldErrors.length; j++) {
                fieldErrors[j].parentNode.removeChild(fieldErrors[j]);
            }
        }

        function closeModal() {
            if (modal) modal.setAttribute('hidden', '');
            if (addSpotForm) addSpotForm.reset();
            clearModalFieldErrors();
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

                clearModalFieldErrors();
                if (modalErrors) {
                    modalErrors.setAttribute('hidden', '');
                    modalErrors.innerHTML = '';
                }

                var fd = new FormData(addSpotForm);
                var decimalFields = ['LengthMeters', 'WidthMeters', 'DepthMeters', 'PricePerDay'];
                decimalFields.forEach(function (name) {
                    var v = fd.get(name);
                    if (typeof v === 'string' && v.indexOf(',') !== -1) {
                        fd.set(name, v.replace(/,/g, '.'));
                    }
                });
                var payload = new URLSearchParams(fd);

                fetch(spotCreateUrl, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: payload
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
                                layer.draw();
                                appendSpotToSidebar({ id: data.id, name: data.name });
                                closeModal();
                            });
                        } else {
                            return r.json().then(function (errors) {
                                var fallbackMessages = [];
                                if (errors && typeof errors === 'object') {
                                    Object.keys(errors).forEach(function (key) {
                                        var errs = errors[key];
                                        var firstMsg = '';
                                        if (Array.isArray(errs) && errs.length > 0) {
                                            firstMsg = errs[0];
                                        } else if (typeof errs === 'string') {
                                            firstMsg = errs;
                                        }
                                        if (!firstMsg) return;

                                        var input = addSpotForm.querySelector('[name="' + key + '"]');
                                        if (input) {
                                            input.setAttribute('aria-invalid', 'true');
                                            var errorEl = document.createElement('p');
                                            errorEl.className = 'field__error';
                                            errorEl.textContent = firstMsg;
                                            input.parentNode.insertBefore(errorEl, input.nextSibling);
                                        } else {
                                            fallbackMessages.push(firstMsg);
                                        }
                                    });
                                }
                                if (fallbackMessages.length === 0 && !addSpotForm.querySelector('[aria-invalid]')) {
                                    fallbackMessages.push('An error occurred. Please try again.');
                                }
                                if (fallbackMessages.length > 0) {
                                    showModalErrors(fallbackMessages);
                                }
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

        var deleteModal = document.getElementById('delete-spot-modal');
        var deleteModalErrors = deleteModal ? deleteModal.querySelector('.modal__errors') : null;
        var deleteModalName = document.getElementById('delete-spot-modal-name');
        var deleteConfirmBtn = document.getElementById('delete-spot-confirm');
        var pendingDeleteSpotId = null;

        function openDeleteModal(spotId, spotName) {
            pendingDeleteSpotId = spotId;
            if (deleteModalName) deleteModalName.textContent = spotName;
            if (deleteModalErrors) {
                deleteModalErrors.setAttribute('hidden', '');
                deleteModalErrors.innerHTML = '';
            }
            if (deleteModal) deleteModal.removeAttribute('hidden');
        }

        function closeDeleteModal() {
            pendingDeleteSpotId = null;
            if (deleteModal) deleteModal.setAttribute('hidden', '');
        }

        if (deleteModal) {
            deleteModal.addEventListener('click', function (e) {
                if (e.target.hasAttribute('data-modal-dismiss')) {
                    closeDeleteModal();
                }
            });
        }

        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.spot-sidebar__delete');
            if (!btn) return;
            openDeleteModal(btn.dataset.spotId, btn.dataset.spotName);
        });

        if (deleteConfirmBtn) {
            deleteConfirmBtn.addEventListener('click', function () {
                if (!pendingDeleteSpotId) return;
                var spotId = pendingDeleteSpotId;
                var url = '/placeowner/marinas/' + marinaId + '/spots/' + spotId + '/delete';

                fetch(url, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'RequestVerificationToken': token
                    }
                })
                    .then(function (r) {
                        if (r.ok) {
                            removeSpotFromUI(spotId);
                            closeDeleteModal();
                        } else {
                            return r.json().then(function (body) {
                                var msg = (body && body.error) ? body.error : 'Delete failed. Please try again.';
                                if (deleteModalErrors) {
                                    deleteModalErrors.removeAttribute('hidden');
                                    deleteModalErrors.innerHTML = '<p>' + escapeHtml(msg) + '</p>';
                                }
                            }).catch(function () {
                                if (deleteModalErrors) {
                                    deleteModalErrors.removeAttribute('hidden');
                                    deleteModalErrors.innerHTML = '<p>Delete failed. Please try again.</p>';
                                }
                            });
                        }
                    })
                    .catch(function () {
                        if (deleteModalErrors) {
                            deleteModalErrors.removeAttribute('hidden');
                            deleteModalErrors.innerHTML = '<p>Network error. Please check your connection and try again.</p>';
                        }
                    });
            });
        }

        function removeSpotFromUI(spotId) {
            var entry = spotsById.get(spotId);
            if (entry) {
                if (transformer.nodes().indexOf(entry.node) !== -1) {
                    transformer.nodes([]);
                }
                entry.node.destroy();
                if (entry.label) entry.label.destroy();
                spotsById.delete(spotId);
                layer.batchDraw();
            }

            var sidebar = document.getElementById('spot-sidebar');
            if (!sidebar) return;

            var item = sidebar.querySelector('.spot-sidebar__item[data-spot-id="' + spotId + '"]');
            if (item && item.parentNode) item.parentNode.removeChild(item);

            var countEl = sidebar.querySelector('.spot-sidebar__count');
            if (countEl) {
                var current = parseInt(countEl.textContent, 10) || 0;
                countEl.textContent = Math.max(0, current - 1).toString();
            }

            var list = sidebar.querySelector('.spot-sidebar__list');
            if (list && list.children.length === 0) {
                var empty = document.createElement('p');
                empty.className = 'spot-sidebar__empty';
                empty.innerHTML = 'No spots yet. Use <em>Add spot</em> in the toolbar to define your first.';
                list.parentNode.replaceChild(empty, list);
            }
        }
    });

    var btnFullscreen = document.getElementById('btn-fullscreen-toggle');
    var workspaceEl = document.querySelector('.workspace--editor');

    function moveButtonsToSidebar() {
        var sidebar = document.getElementById('spot-sidebar');
        if (!sidebar) return;
        var btnAdd = document.getElementById('btn-add-spot');
        var btnSave = document.getElementById('btn-save-layout');
        if (!btnAdd || !btnSave || !btnFullscreen) return;

        var container = sidebar.querySelector('.spot-sidebar__toolbar');
        if (!container) {
            container = document.createElement('div');
            container.className = 'spot-sidebar__toolbar';
            sidebar.insertBefore(container, sidebar.firstChild);
        }
        container.appendChild(btnAdd);
        container.appendChild(btnFullscreen);
        container.appendChild(btnSave);
    }

    function moveButtonsBackToToolbar() {
        var toolbar = document.querySelector('.workspace__head .toolbar');
        var sidebar = document.getElementById('spot-sidebar');
        if (!sidebar) return;
        var container = sidebar.querySelector('.spot-sidebar__toolbar');
        if (!container || !toolbar) return;

        var btnAdd = document.getElementById('btn-add-spot');
        var btnSave = document.getElementById('btn-save-layout');
        if (btnAdd) toolbar.appendChild(btnAdd);
        if (btnFullscreen) toolbar.appendChild(btnFullscreen);
        if (btnSave) toolbar.appendChild(btnSave);

        container.remove();
    }

    function isFullscreen() {
        return workspaceEl && workspaceEl.classList.contains('workspace--fullscreen');
    }

    function enterFullscreen() {
        if (!workspaceEl) return;
        workspaceEl.classList.add('workspace--fullscreen');
        document.body.classList.add('body--fullscreen-editor');
        moveButtonsToSidebar();
        if (btnFullscreen) btnFullscreen.textContent = 'Exit fullscreen';
    }

    function exitFullscreen() {
        if (!workspaceEl) return;
        moveButtonsBackToToolbar();
        workspaceEl.classList.remove('workspace--fullscreen');
        document.body.classList.remove('body--fullscreen-editor');
        if (btnFullscreen) btnFullscreen.textContent = 'Fullscreen';
    }

    if (btnFullscreen) {
        btnFullscreen.addEventListener('click', function () {
            if (isFullscreen()) {
                exitFullscreen();
            } else {
                enterFullscreen();
            }
        });
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var addModalOpen = modal && !modal.hasAttribute('hidden');
        var deleteModalOpen = deleteModal && !deleteModal.hasAttribute('hidden');
        if (addModalOpen || deleteModalOpen) return;
        if (isFullscreen()) {
            exitFullscreen();
        }
    });
}());
