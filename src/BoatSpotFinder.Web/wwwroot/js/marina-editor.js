(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const uploadForm = document.getElementById('background-upload-form');
        const uploadInput = uploadForm ? uploadForm.querySelector('input[name="backgroundImage"]') : null;
        const uploadError = document.getElementById('background-upload-error');

        if (uploadForm && uploadInput && uploadError) {
            uploadInput.addEventListener('change', function () {
                uploadError.textContent = '';
                uploadError.hidden = true;
            });

            uploadForm.addEventListener('submit', function (e) {
                const file = uploadInput.files && uploadInput.files[0];
                if (!file) { return; }

                const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
                const allowedExts = ['.jpg', '.jpeg', '.png', '.webp'];
                const maxSize = 5 * 1024 * 1024;
                let errorMsg = '';

                if (allowedTypes.indexOf(file.type) === -1) {
                    errorMsg = 'Please choose a JPG, PNG, or WebP image.';
                } else {
                    const nameLower = file.name.toLowerCase();
                    let extOk = false;
                    for (let i = 0; i < allowedExts.length; i++) {
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

        const container = document.getElementById('canvas-container');
        if (!container) return;

        const marinaId = container.dataset.marinaId;
        const spotCreateUrl = container.dataset.spotCreateUrl;
        const stageWidth = container.clientWidth;
        const stageHeight = container.clientHeight;
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        const stage = new Konva.Stage({
            container: 'canvas-container',
            width: stageWidth,
            height: stageHeight
        });

        const layer = new Konva.Layer();
        stage.add(layer);

        const transformer = new Konva.Transformer({
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

        const spotsById = new Map();
        let unplacedCount = 0;
        const layoutBounds = { w: 0, h: 0 };

        const SNAP_THRESHOLD = 8;

        function getOtherRects(draggedNode) {
            const rects = [];
            spotsById.forEach(function (entry) {
                if (entry.node !== draggedNode) {
                    rects.push(entry.node.getClientRect({ relativeTo: layer, skipStroke: true }));
                }
            });
            return rects;
        }

        const OVERLAP_EPSILON = 0.5;

        function rectsOverlap(a, b) {
            return !(a.x + a.width <= b.x + OVERLAP_EPSILON ||
                     b.x + b.width <= a.x + OVERLAP_EPSILON ||
                     a.y + a.height <= b.y + OVERLAP_EPSILON ||
                     b.y + b.height <= a.y + OVERLAP_EPSILON);
        }

        function isOutOfBounds(r) {
            return r.x < -OVERLAP_EPSILON || r.y < -OVERLAP_EPSILON ||
                   r.x + r.width > layoutBounds.w + OVERLAP_EPSILON ||
                   r.y + r.height > layoutBounds.h + OVERLAP_EPSILON;
        }

        function findEmptySlot(w, h) {
            if (layoutBounds.w <= 0 || layoutBounds.h <= 0) {
                return null;
            }

            const existing = [];
            spotsById.forEach(function (entry) {
                existing.push(entry.node.getClientRect({ relativeTo: layer, skipStroke: true }));
            });

            const step = 20;
            for (let y = 20; y + h <= layoutBounds.h - 20; y += step) {
                for (let x = 20; x + w <= layoutBounds.w - 20; x += step) {
                    const candidate = { x: x, y: y, width: w, height: h };
                    let clash = false;
                    for (let i = 0; i < existing.length; i++) {
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
            const aabb = node.getClientRect({ relativeTo: layer, skipStroke: true });
            const others = getOtherRects(node);

            const start = node._preDragAABB;
            const startLeft   = start ? start.x : null;
            const startRight  = start ? start.x + start.width : null;
            const startTop    = start ? start.y : null;
            const startBottom = start ? start.y + start.height : null;

            let bestDx = null;
            let bestDy = null;

            function trySnapX(myEdge, targetPos, startEdge) {
                const delta = targetPos - myEdge;
                if (Math.abs(delta) > SNAP_THRESHOLD) return;
                if (startEdge !== null && Math.abs(targetPos - startEdge) < 0.5) return;
                if (bestDx === null || Math.abs(delta) < Math.abs(bestDx)) bestDx = delta;
            }

            function trySnapY(myEdge, targetPos, startEdge) {
                const delta = targetPos - myEdge;
                if (Math.abs(delta) > SNAP_THRESHOLD) return;
                if (startEdge !== null && Math.abs(targetPos - startEdge) < 0.5) return;
                if (bestDy === null || Math.abs(delta) < Math.abs(bestDy)) bestDy = delta;
            }

            const myLeft = aabb.x;
            const myRight = aabb.x + aabb.width;
            const myTop = aabb.y;
            const myBottom = aabb.y + aabb.height;

            trySnapX(myLeft,   0,              startLeft);
            trySnapX(myRight,  layoutBounds.w, startRight);
            trySnapY(myTop,    0,              startTop);
            trySnapY(myBottom, layoutBounds.h, startBottom);

            for (let i = 0; i < others.length; i++) {
                const o = others[i];

                trySnapX(myLeft,   o.x + o.width,  startLeft);
                trySnapX(myRight,  o.x,             startRight);
                trySnapY(myTop,    o.y + o.height,  startTop);
                trySnapY(myBottom, o.y,             startBottom);
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
            const draggedNode = transformer.nodes()[0];
            const others = getOtherRects(draggedNode);

            const stroke = (draggedNode.strokeWidth && draggedNode.strokeWidth()) || 0;
            const halfStroke = stroke / 2;

            const oldLeft   = oldBox.x + halfStroke;
            const oldRight  = oldBox.x + oldBox.width  - halfStroke;
            const oldTop    = oldBox.y + halfStroke;
            const oldBottom = oldBox.y + oldBox.height - halfStroke;
            let newLeft     = newBox.x + halfStroke;
            let newRight    = newBox.x + newBox.width  - halfStroke;
            let newTop      = newBox.y + halfStroke;
            let newBottom   = newBox.y + newBox.height - halfStroke;

            const leftChanged   = Math.abs(newLeft   - oldLeft)   > 0.5;
            const rightChanged  = Math.abs(newRight  - oldRight)  > 0.5;
            const topChanged    = Math.abs(newTop    - oldTop)    > 0.5;
            const bottomChanged = Math.abs(newBottom - oldBottom) > 0.5;

            function pickBestSnap(value, oldValue, targets) {
                if (value === oldValue) return null;
                const direction = Math.sign(value - oldValue);
                let best = null;
                for (let i = 0; i < targets.length; i++) {
                    const t = targets[i];
                    const offset = t - oldValue;
                    if (Math.abs(offset) < 0.5 || Math.sign(offset) !== direction) continue;
                    const d = t - value;
                    if (Math.abs(d) < SNAP_THRESHOLD && (best === null || Math.abs(d) < Math.abs(best))) {
                        best = d;
                    }
                }
                return best;
            }

            if (rightChanged) {
                const rightTargets = [layoutBounds.w];
                for (let i = 0; i < others.length; i++) rightTargets.push(others[i].x);
                const d = pickBestSnap(newRight, oldRight, rightTargets);
                if (d !== null) newRight = newRight + d;
            }
            if (leftChanged) {
                const leftTargets = [0];
                for (let j = 0; j < others.length; j++) leftTargets.push(others[j].x + others[j].width);
                const d2 = pickBestSnap(newLeft, oldLeft, leftTargets);
                if (d2 !== null) newLeft = newLeft + d2;
            }
            if (bottomChanged) {
                const bottomTargets = [layoutBounds.h];
                for (let k = 0; k < others.length; k++) bottomTargets.push(others[k].y);
                const d3 = pickBestSnap(newBottom, oldBottom, bottomTargets);
                if (d3 !== null) newBottom = newBottom + d3;
            }
            if (topChanged) {
                const topTargets = [0];
                for (let m = 0; m < others.length; m++) topTargets.push(others[m].y + others[m].height);
                const d4 = pickBestSnap(newTop, oldTop, topTargets);
                if (d4 !== null) newTop = newTop + d4;
            }

            const snappedGeom = {
                x:      newLeft,
                y:      newTop,
                width:  newRight  - newLeft,
                height: newBottom - newTop
            };

            if (snappedGeom.width <= 0 || snappedGeom.height <= 0) return oldBox;
            if (snappedGeom.x < 0 || snappedGeom.y < 0 ||
                snappedGeom.x + snappedGeom.width  > layoutBounds.w ||
                snappedGeom.y + snappedGeom.height > layoutBounds.h) {
                return oldBox;
            }
            for (let n = 0; n < others.length; n++) {
                if (rectsOverlap(snappedGeom, others[n])) {
                    return oldBox;
                }
            }

            const snapped = {
                x:        newLeft   - halfStroke,
                y:        newTop    - halfStroke,
                width:    (newRight  - newLeft)  + stroke,
                height:   (newBottom - newTop)   + stroke,
                rotation: newBox.rotation
            };

            return snapped;
        }

        function addSpotToCanvas(spot) {
            const placed = spot.canvasX != null && spot.canvasY != null && spot.canvasW != null && spot.canvasH != null;
            let x, y, w, h, rotation, dashed, fill, stroke, opacity;

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
                const slot = findEmptySlot(w, h);
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

            const rect = new Konva.Rect({
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

            const label = new Konva.Text({
                text: spot.name,
                fontSize: 11,
                fontFamily: 'Manrope, sans-serif',
                fill: '#ffffff',
                listening: false
            });

            function positionLabel() {
                const rw = rect.width() * rect.scaleX();
                const rh = rect.height() * rect.scaleY();
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
                rect._preDragAABB = rect.getClientRect({ relativeTo: layer, skipStroke: true });
            });

            rect.on('dragmove', function () {
                applySnapDuringDrag(rect);
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('dragend', function () {
                const aabb = rect.getClientRect({ relativeTo: layer, skipStroke: true });
                const others = getOtherRects(rect);
                let bad = isOutOfBounds(aabb);
                if (!bad) {
                    for (let i = 0; i < others.length; i++) {
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
                rect._preTransformAABB = rect.getClientRect({ relativeTo: layer, skipStroke: true });
            });

            rect.on('transform', function () {
                updateLabelPosition();
                layer.batchDraw();
            });

            rect.on('transformend', function () {
                const aabb = rect.getClientRect({ relativeTo: layer, skipStroke: true });
                const others = getOtherRects(rect);
                let bad = isOutOfBounds(aabb);
                if (!bad) {
                    for (let i = 0; i < others.length; i++) {
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
            const sidebar = document.getElementById('spot-sidebar');
            if (!sidebar) return;

            const countEl = sidebar.querySelector('.spot-sidebar__count');
            if (countEl) {
                const current = parseInt(countEl.textContent, 10) || 0;
                countEl.textContent = (current + 1).toString();
            }

            const emptyEl = sidebar.querySelector('.spot-sidebar__empty');
            if (emptyEl) {
                emptyEl.parentNode.removeChild(emptyEl);
            }

            let list = sidebar.querySelector('.spot-sidebar__list');
            if (!list) {
                list = document.createElement('ul');
                list.className = 'spot-sidebar__list';
                sidebar.appendChild(list);
            }

            const editUrl = container.dataset.spotEditUrlTemplate
                ? container.dataset.spotEditUrlTemplate.replace('__ID__', spot.id)
                : '#';

            const li = document.createElement('li');
            li.className = 'spot-sidebar__item';
            li.dataset.spotId = spot.id;

            const row = document.createElement('div');
            row.className = 'spot-sidebar__row';

            const nameSpan = document.createElement('span');
            nameSpan.className = 'spot-sidebar__name';
            nameSpan.textContent = spot.name;
            row.appendChild(nameSpan);

            const pill = document.createElement('span');
            pill.className = 'pill pill--unplaced';
            const dot = document.createElement('span');
            dot.className = 'pill__dot';
            dot.setAttribute('aria-hidden', 'true');
            pill.appendChild(dot);
            pill.appendChild(document.createTextNode('Unplaced'));
            row.appendChild(pill);

            li.appendChild(row);

            const actions = document.createElement('div');
            actions.className = 'spot-sidebar__actions';

            const editLink = document.createElement('a');
            editLink.className = 'spot-sidebar__edit';
            editLink.href = editUrl;
            editLink.textContent = 'Edit details →';
            actions.appendChild(editLink);

            const deleteBtn = document.createElement('button');
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

        const modal = document.getElementById('add-spot-modal');
        const addSpotForm = document.getElementById('add-spot-form');
        const modalErrors = modal ? modal.querySelector('.modal__errors') : null;

        function openModal() {
            if (modal) modal.removeAttribute('hidden');
        }

        function clearModalFieldErrors() {
            if (!addSpotForm) return;
            const inputs = addSpotForm.querySelectorAll('[aria-invalid]');
            for (let i = 0; i < inputs.length; i++) {
                inputs[i].removeAttribute('aria-invalid');
            }
            const fieldErrors = addSpotForm.querySelectorAll('.field__error');
            for (let j = 0; j < fieldErrors.length; j++) {
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

        const btnAddSpot = document.getElementById('btn-add-spot');
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

                const fd = new FormData(addSpotForm);
                const decimalFields = ['LengthMeters', 'WidthMeters', 'DepthMeters', 'PricePerDay'];
                decimalFields.forEach(function (name) {
                    const v = fd.get(name);
                    if (typeof v === 'string' && v.indexOf(',') !== -1) {
                        fd.set(name, v.replace(/,/g, '.'));
                    }
                });
                const payload = new URLSearchParams(fd);

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
                                const fallbackMessages = [];
                                if (errors && typeof errors === 'object') {
                                    Object.keys(errors).forEach(function (key) {
                                        const errs = errors[key];
                                        let firstMsg = '';
                                        if (Array.isArray(errs) && errs.length > 0) {
                                            firstMsg = errs[0];
                                        } else if (typeof errs === 'string') {
                                            firstMsg = errs;
                                        }
                                        if (!firstMsg) return;

                                        const input = addSpotForm.querySelector('[name="' + key + '"]');
                                        if (input) {
                                            input.setAttribute('aria-invalid', 'true');
                                            const errorEl = document.createElement('p');
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

        const btnSaveLayout = document.getElementById('btn-save-layout');
        if (btnSaveLayout) {
            btnSaveLayout.addEventListener('click', function () {
                const positions = [];
                spotsById.forEach(function (entry, id) {
                    const node = entry.node;
                    const newW = node.width() * node.scaleX();
                    const newH = node.height() * node.scaleY();
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
                            const btnText = btnSaveLayout.textContent;
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

        const deleteModal = document.getElementById('delete-spot-modal');
        const deleteModalErrors = deleteModal ? deleteModal.querySelector('.modal__errors') : null;
        const deleteModalName = document.getElementById('delete-spot-modal-name');
        const deleteConfirmBtn = document.getElementById('delete-spot-confirm');
        let pendingDeleteSpotId = null;

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
            const btn = e.target.closest('.spot-sidebar__delete');
            if (!btn) return;
            openDeleteModal(btn.dataset.spotId, btn.dataset.spotName);
        });

        if (deleteConfirmBtn) {
            deleteConfirmBtn.addEventListener('click', function () {
                if (!pendingDeleteSpotId) return;
                const spotId = pendingDeleteSpotId;
                const url = '/placeowner/marinas/' + marinaId + '/spots/' + spotId + '/delete';

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
                                const msg = (body && body.error) ? body.error : 'Delete failed. Please try again.';
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
            const entry = spotsById.get(spotId);
            if (entry) {
                if (transformer.nodes().indexOf(entry.node) !== -1) {
                    transformer.nodes([]);
                }
                entry.node.destroy();
                if (entry.label) entry.label.destroy();
                spotsById.delete(spotId);
                layer.batchDraw();
            }

            const sidebar = document.getElementById('spot-sidebar');
            if (!sidebar) return;

            const item = sidebar.querySelector('.spot-sidebar__item[data-spot-id="' + spotId + '"]');
            if (item && item.parentNode) item.parentNode.removeChild(item);

            const countEl = sidebar.querySelector('.spot-sidebar__count');
            if (countEl) {
                const current = parseInt(countEl.textContent, 10) || 0;
                countEl.textContent = Math.max(0, current - 1).toString();
            }

            const list = sidebar.querySelector('.spot-sidebar__list');
            if (list && list.children.length === 0) {
                const empty = document.createElement('p');
                empty.className = 'spot-sidebar__empty';
                empty.innerHTML = 'No spots yet. Use <em>Add spot</em> in the toolbar to define your first.';
                list.parentNode.replaceChild(empty, list);
            }
        }

        const clearBgForm = document.getElementById('clear-background-form');
        const clearBgModal = document.getElementById('clear-background-modal');
        const clearBgModalErrors = clearBgModal ? clearBgModal.querySelector('.modal__errors') : null;
        const clearBgConfirmBtn = document.getElementById('clear-background-confirm');
        let clearBgUrl = null;

        function openClearBgModal(url) {
            clearBgUrl = url;
            if (clearBgModalErrors) {
                clearBgModalErrors.setAttribute('hidden', '');
                clearBgModalErrors.innerHTML = '';
            }
            if (clearBgModal) clearBgModal.removeAttribute('hidden');
        }

        function closeClearBgModal() {
            if (clearBgModal) clearBgModal.setAttribute('hidden', '');
            clearBgUrl = null;
        }

        if (clearBgForm) {
            clearBgForm.addEventListener('submit', function (e) {
                e.preventDefault();
                openClearBgModal(clearBgForm.action);
            });
        }

        if (clearBgModal) {
            clearBgModal.addEventListener('click', function (e) {
                if (e.target.closest('[data-modal-dismiss]')) closeClearBgModal();
            });
        }

        if (clearBgConfirmBtn) {
            clearBgConfirmBtn.addEventListener('click', function () {
                if (!clearBgUrl) return;
                fetch(clearBgUrl, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'RequestVerificationToken': token
                    }
                })
                    .then(function (r) {
                        if (r.ok) {
                            closeClearBgModal();
                            window.location.reload();
                        } else {
                            return r.json().then(function (body) {
                                const msg = (body && body.error) ? body.error : 'Failed to remove background. Please try again.';
                                if (clearBgModalErrors) {
                                    clearBgModalErrors.removeAttribute('hidden');
                                    clearBgModalErrors.innerHTML = '<p>' + escapeHtml(msg) + '</p>';
                                }
                            }).catch(function () {
                                if (clearBgModalErrors) {
                                    clearBgModalErrors.removeAttribute('hidden');
                                    clearBgModalErrors.innerHTML = '<p>Failed to remove background. Please try again.</p>';
                                }
                            });
                        }
                    })
                    .catch(function () {
                        if (clearBgModalErrors) {
                            clearBgModalErrors.removeAttribute('hidden');
                            clearBgModalErrors.innerHTML = '<p>Network error. Please try again.</p>';
                        }
                    });
            });
        }
    });

    const btnFullscreen = document.getElementById('btn-fullscreen-toggle');
    const workspaceEl = document.querySelector('.workspace--editor');

    function moveButtonsToSidebar() {
        const sidebar = document.getElementById('spot-sidebar');
        if (!sidebar) return;
        const btnAdd = document.getElementById('btn-add-spot');
        const btnSave = document.getElementById('btn-save-layout');
        if (!btnAdd || !btnSave || !btnFullscreen) return;

        let container = sidebar.querySelector('.spot-sidebar__toolbar');
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
        const toolbar = document.querySelector('.workspace__head .toolbar');
        const sidebar = document.getElementById('spot-sidebar');
        if (!sidebar) return;
        const container = sidebar.querySelector('.spot-sidebar__toolbar');
        if (!container || !toolbar) return;

        const btnAdd = document.getElementById('btn-add-spot');
        const btnSave = document.getElementById('btn-save-layout');
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
        const addModalOpen = modal && !modal.hasAttribute('hidden');
        const deleteModalOpen = deleteModal && !deleteModal.hasAttribute('hidden');
        const clearBgModalOpen = clearBgModal && !clearBgModal.hasAttribute('hidden');
        if (addModalOpen || deleteModalOpen || clearBgModalOpen) return;
        if (isFullscreen()) {
            exitFullscreen();
        }
    });
}());
