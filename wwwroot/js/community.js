/**
 * KrishiLink Community Hub Client-Side Script
 * Strict Zero-Emoji Standard | AJAX Reactions, Centered Comments Modal & Audio
 */

document.addEventListener("DOMContentLoaded", function () {
    initComposerTypeToggle();
    initComposerMediaPreview();
    initVoiceRecorder();
    initAudioPlayers();
    initReactionButtons();
    initCommentForms();
    initAcceptSolutionButtons();
    initBookmarkButtons();
    initCopyLinkButtons();
    initCommentsModal();
    initReportPostButtons();
});

/**
 * 1. Post Composer Type Switcher (Help Needed Triage Form Visibility)
 */
function initComposerTypeToggle() {
    const postTypeRadios = document.querySelectorAll("input[name='PostType']");
    const helpContainer = document.getElementById("helpNeededFieldsContainer");

    if (!postTypeRadios.length || !helpContainer) return;

    function updateVisibility() {
        const selected = document.querySelector("input[name='PostType']:checked");
        if (selected && selected.value === "HelpNeeded") {
            helpContainer.style.display = "block";
            helpContainer.querySelectorAll("select, input").forEach(el => {
                if (el.name === "CropCategory" || el.name === "IssueCategory") {
                    el.required = true;
                }
            });
        } else {
            helpContainer.style.display = "none";
            helpContainer.querySelectorAll("select, input").forEach(el => {
                el.required = false;
            });
        }
    }

    postTypeRadios.forEach(r => r.addEventListener("change", updateVisibility));
    updateVisibility();

    // Modal Trigger Shortcuts from in-page composer card
    const composerModal = document.getElementById("createPostModal");
    if (composerModal) {
        composerModal.addEventListener("show.bs.modal", function (event) {
            const button = event.relatedTarget;
            if (!button) return;
            const tab = button.getAttribute("data-post-tab");
            if (tab === "HelpNeeded") {
                const radio = document.getElementById("typeHelpNeeded");
                if (radio) { radio.checked = true; updateVisibility(); }
            } else if (tab === "Experience") {
                const radio = document.getElementById("typeExperience");
                if (radio) { radio.checked = true; updateVisibility(); }
            } else if (tab === "Voice") {
                const radio = document.getElementById("typeExperience");
                if (radio) { radio.checked = true; updateVisibility(); }
                setTimeout(() => {
                    const startBtn = document.getElementById("startVoiceRecordingBtn");
                    if (startBtn) startBtn.click();
                }, 300);
            }
        });
    }
}

/**
 * 2. Composer Media File Preview (Images & Video)
 */
function initComposerMediaPreview() {
    const imageInput = document.getElementById("composerImageFileInput");
    const tray = document.getElementById("composerMediaPreviewTray");

    if (!imageInput || !tray) return;

    imageInput.addEventListener("change", function () {
        tray.innerHTML = "";
        const files = Array.from(this.files).slice(0, 5);

        files.forEach((file, idx) => {
            if (!file.type.startsWith("image/")) return;

            const reader = new FileReader();
            reader.onload = function (e) {
                const thumb = document.createElement("div");
                thumb.className = "preview-thumb-container";
                thumb.innerHTML = `
                    <img src="${e.target.result}" alt="Preview ${idx + 1}" />
                    <button type="button" class="remove-thumb-btn" title="Remove" data-index="${idx}">
                        <i class="bi bi-x"></i>
                    </button>
                `;
                tray.appendChild(thumb);

                thumb.querySelector(".remove-thumb-btn").addEventListener("click", function () {
                    thumb.remove();
                });
            };
            reader.readAsDataURL(file);
        });
    });

    const videoInput = document.getElementById("composerVideoFileInput");
    if (videoInput) {
        videoInput.addEventListener("change", function () {
            const existingVideoThumb = tray.querySelector(".preview-video-thumb");
            if (existingVideoThumb) existingVideoThumb.remove();

            if (this.files.length > 0) {
                const file = this.files[0];
                const thumb = document.createElement("div");
                thumb.className = "preview-thumb-container preview-video-thumb d-flex flex-column align-items-center justify-content-center p-2 bg-dark text-white rounded-3 position-relative";
                thumb.style.width = "120px";
                thumb.style.height = "90px";
                thumb.innerHTML = `
                    <i class="bi bi-camera-reels-fill fs-3 text-warning mb-1"></i>
                    <span class="small text-truncate w-100 text-center" style="font-size: 0.75rem;" title="${file.name}">${file.name}</span>
                    <button type="button" class="remove-thumb-btn position-absolute top-0 end-0 m-1" title="Remove">
                        <i class="bi bi-x"></i>
                    </button>
                `;
                tray.appendChild(thumb);

                thumb.querySelector(".remove-thumb-btn").addEventListener("click", function () {
                    thumb.remove();
                    videoInput.value = "";
                });
            }
        });
    }
}

/**
 * 3. Live Audio Voice Note Recorder
 */
let mediaRecorderInstance = null;
let audioChunks = [];
let voiceTimerInterval = null;
let voiceSeconds = 0;

function initVoiceRecorder() {
    const startBtn = document.getElementById("startVoiceRecordingBtn");
    const previewTray = document.getElementById("composerVoicePreview");
    const timerDisplay = document.getElementById("voiceRecordedTimer");
    const deleteBtn = document.getElementById("deleteVoiceRecordingBtn");

    if (!startBtn || !previewTray) return;

    startBtn.addEventListener("click", async function () {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alert("আপনার ব্রাউজারে ভয়েস রেকর্ড সমর্থন করে না।");
            return;
        }

        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaRecorderInstance = new MediaRecorder(stream);
            audioChunks = [];
            voiceSeconds = 0;

            mediaRecorderInstance.ondataavailable = function (e) {
                if (e.data.size > 0) {
                    audioChunks.push(e.data);
                }
            };

            mediaRecorderInstance.onstop = function () {
                const audioBlob = new Blob(audioChunks, { type: "audio/webm" });
                const audioFile = new File([audioBlob], `voice_note_${Date.now()}.webm`, { type: "audio/webm" });

                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(audioFile);

                let audioInput = document.getElementById("hiddenAudioFileInput");
                if (!audioInput) {
                    audioInput = document.createElement("input");
                    audioInput.type = "file";
                    audioInput.name = "AudioFile";
                    audioInput.id = "hiddenAudioFileInput";
                    audioInput.style.display = "none";
                    const form = document.getElementById("mainPostCreateForm");
                    if (form) form.appendChild(audioInput);
                }
                audioInput.files = dataTransfer.files;

                let durationInput = document.getElementById("hiddenAudioDurationInput");
                if (!durationInput) {
                    durationInput = document.createElement("input");
                    durationInput.type = "hidden";
                    durationInput.name = "AudioDurationSeconds";
                    durationInput.id = "hiddenAudioDurationInput";
                    const form = document.getElementById("mainPostCreateForm");
                    if (form) form.appendChild(durationInput);
                }
                durationInput.value = voiceSeconds;

                previewTray.style.setProperty("display", "flex", "important");
                if (timerDisplay) {
                    const mins = Math.floor(voiceSeconds / 60);
                    const secs = voiceSeconds % 60;
                    timerDisplay.textContent = `${mins}:${secs < 10 ? '0' : ''}${secs}`;
                }

                stream.getTracks().forEach(t => t.stop());
            };

            mediaRecorderInstance.start();
            startBtn.classList.remove("btn-outline-primary");
            startBtn.classList.add("btn-danger");
            startBtn.innerHTML = '<i class="bi bi-stop-circle me-2"></i> <span>রেকর্ডিং বন্ধ করুন</span>';

            voiceTimerInterval = setInterval(() => {
                voiceSeconds++;
                if (voiceSeconds >= 60) {
                    stopRecording();
                }
            }, 1000);

            startBtn.onclick = stopRecording;

        } catch (err) {
            console.error("Microphone access error:", err);
            alert("মাইক্রোফোন ব্যবহারের অনুমতি পাওয়া যায়নি।");
        }
    });

    function stopRecording() {
        if (mediaRecorderInstance && mediaRecorderInstance.state === "recording") {
            mediaRecorderInstance.stop();
        }
        clearInterval(voiceTimerInterval);
        startBtn.classList.remove("btn-danger");
        startBtn.classList.add("btn-outline-primary");
        startBtn.innerHTML = '<i class="bi bi-mic me-2"></i> <span>ভয়েস রেকর্ড</span>';
        startBtn.onclick = null;
    }

    if (deleteBtn) {
        deleteBtn.addEventListener("click", function () {
            audioChunks = [];
            const audioInput = document.getElementById("hiddenAudioFileInput");
            if (audioInput) audioInput.value = "";
            const durationInput = document.getElementById("hiddenAudioDurationInput");
            if (durationInput) durationInput.value = "0";
            previewTray.style.setProperty("display", "none", "important");
        });
    }
}

/**
 * 4. Audio Voice Note Players
 */
let currentActiveAudio = null;
let currentActiveBtn = null;

function initAudioPlayers() {
    document.querySelectorAll(".audio-play-btn").forEach(btn => {
        btn.addEventListener("click", function () {
            const audioSrc = this.getAttribute("data-audio-src");
            if (!audioSrc) return;

            const icon = this.querySelector("i");
            const playerCard = this.closest(".community-audio-player, .comment-audio-attachment");
            const progressBar = playerCard ? playerCard.querySelector(".audio-progress") : null;
            const timeDisplay = playerCard ? playerCard.querySelector(".audio-time") : null;

            if (currentActiveAudio && currentActiveAudio.src.includes(audioSrc)) {
                if (!currentActiveAudio.paused) {
                    currentActiveAudio.pause();
                    if (icon) { icon.classList.remove("bi-pause-fill"); icon.classList.add("bi-play-fill"); }
                    return;
                } else {
                    currentActiveAudio.play();
                    if (icon) { icon.classList.remove("bi-play-fill"); icon.classList.add("bi-pause-fill"); }
                    return;
                }
            }

            // Stop any currently playing audio
            if (currentActiveAudio) {
                currentActiveAudio.pause();
                if (currentActiveBtn) {
                    const prevIcon = currentActiveBtn.querySelector("i");
                    if (prevIcon) { prevIcon.classList.remove("bi-pause-fill"); prevIcon.classList.add("bi-play-fill"); }
                }
            }

            const audio = new Audio(audioSrc);
            currentActiveAudio = audio;
            currentActiveBtn = this;

            if (icon) { icon.classList.remove("bi-play-fill"); icon.classList.add("bi-pause-fill"); }

            audio.addEventListener("timeupdate", () => {
                if (progressBar && audio.duration) {
                    const pct = (audio.currentTime / audio.duration) * 100;
                    progressBar.style.width = pct + "%";
                }
                if (timeDisplay && audio.duration) {
                    const curMins = Math.floor(audio.currentTime / 60);
                    const curSecs = Math.floor(audio.currentTime % 60);
                    const totalMins = Math.floor(audio.duration / 60);
                    const totalSecs = Math.floor(audio.duration % 60);
                    timeDisplay.textContent = `${curMins}:${curSecs < 10 ? '0' : ''}${curSecs} / ${totalMins}:${totalSecs < 10 ? '0' : ''}${totalSecs}`;
                }
            });

            audio.addEventListener("ended", () => {
                if (icon) { icon.classList.remove("bi-pause-fill"); icon.classList.add("bi-play-fill"); }
                if (progressBar) progressBar.style.width = "0%";
            });

            audio.play();
        });
    });
}

/**
 * 5. AJAX Reactions (Helpful / Like Toggle)
 */
function initReactionButtons() {
    document.querySelectorAll(".post-reaction-btn, .comment-like-btn").forEach(btn => {
        // avoid duplicate listeners
        if (btn.dataset.reactionBound) return;
        btn.dataset.reactionBound = "true";

        btn.addEventListener("click", async function (e) {
            e.preventDefault();
            if (this.dataset.busy === "true") return;
            this.dataset.busy = "true";

            const postId = this.getAttribute("data-post-id");
            const commentId = this.getAttribute("data-comment-id");

            const tokenInput = document.querySelector("input[name='__RequestVerificationToken']") ||
                               document.querySelector("#antiForgeryForm input[name='__RequestVerificationToken']");
            const token = tokenInput ? tokenInput.value : "";

            const formData = new FormData();
            if (postId) formData.append("postId", postId);
            if (commentId) formData.append("commentId", commentId);
            formData.append("reactionType", "Helpful");
            if (token) formData.append("__RequestVerificationToken", token);

            // Optimistic UI state tracking
            const wasLiked = this.classList.contains("text-success");
            const icon = this.querySelector("i");
            const card = postId ? document.getElementById(`post-card-${postId}`) : null;
            const countDisplay = card ? card.querySelector(".post-like-count-display .count-val") : null;
            const prevCount = countDisplay ? (parseInt(countDisplay.textContent.trim(), 10) || 0) : 0;

            const commentCountSpan = commentId ? this.querySelector(".like-count") : null;
            const prevCommentCount = commentCountSpan ? (parseInt(commentCountSpan.textContent.trim(), 10) || 0) : 0;

            // Apply optimistic UI immediately
            if (wasLiked) {
                this.classList.remove("text-success", "fw-bold");
                this.classList.add("text-muted");
                if (icon) { icon.classList.remove("bi-hand-thumbs-up-fill"); icon.classList.add("bi-hand-thumbs-up"); }
                if (countDisplay) countDisplay.textContent = Math.max(0, prevCount - 1);
                if (commentCountSpan) commentCountSpan.textContent = prevCommentCount > 1 ? (prevCommentCount - 1) : "";
            } else {
                this.classList.remove("text-muted");
                this.classList.add("text-success", "fw-bold");
                if (icon) { icon.classList.remove("bi-hand-thumbs-up"); icon.classList.add("bi-hand-thumbs-up-fill"); }
                if (countDisplay) countDisplay.textContent = prevCount + 1;
                if (commentCountSpan) commentCountSpan.textContent = prevCommentCount + 1;
            }

            const revertUI = () => {
                if (wasLiked) {
                    this.classList.remove("text-muted");
                    this.classList.add("text-success", "fw-bold");
                    if (icon) { icon.classList.remove("bi-hand-thumbs-up"); icon.classList.add("bi-hand-thumbs-up-fill"); }
                    if (countDisplay) countDisplay.textContent = prevCount;
                    if (commentCountSpan) commentCountSpan.textContent = prevCommentCount > 0 ? prevCommentCount : "";
                } else {
                    this.classList.remove("text-success", "fw-bold");
                    this.classList.add("text-muted");
                    if (icon) { icon.classList.remove("bi-hand-thumbs-up-fill"); icon.classList.add("bi-hand-thumbs-up"); }
                    if (countDisplay) countDisplay.textContent = prevCount;
                    if (commentCountSpan) commentCountSpan.textContent = prevCommentCount > 0 ? prevCommentCount : "";
                }
            };

            try {
                const res = await fetch("/Community/ToggleReaction", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": token,
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData
                });

                if (res.ok) {
                    const data = await res.json();
                    if (data.requireLogin) {
                        revertUI();
                        window.location.href = data.redirectUrl || "/Account/Login";
                        return;
                    }

                    if (data.success) {
                        if (postId) {
                            if (countDisplay) countDisplay.textContent = data.likeCount;
                            if (data.action === "Added") {
                                this.classList.remove("text-muted");
                                this.classList.add("text-success", "fw-bold");
                                if (icon) { icon.classList.remove("bi-hand-thumbs-up"); icon.classList.add("bi-hand-thumbs-up-fill"); }
                            } else {
                                this.classList.remove("text-success", "fw-bold");
                                this.classList.add("text-muted");
                                if (icon) { icon.classList.remove("bi-hand-thumbs-up-fill"); icon.classList.add("bi-hand-thumbs-up"); }
                            }
                        } else if (commentId) {
                            if (commentCountSpan) commentCountSpan.textContent = data.likeCount > 0 ? data.likeCount : "";
                        }
                    } else {
                        revertUI();
                    }
                } else if (res.status === 401) {
                    revertUI();
                    window.location.href = "/Account/Login";
                } else {
                    revertUI();
                }
            } catch (err) {
                console.error("Reaction toggle failed:", err);
                revertUI();
            } finally {
                this.dataset.busy = "false";
            }
        });
    });
}

/**
 * 6. Dynamic Centered Comments Modal Box (Open, Fetch, Render & Submit)
 */
function initCommentsModal() {
    const modalContainer = document.getElementById("communityCommentsModal");
    if (!modalContainer) return;

    let bsModalInstance = null;

    // Attach click handlers to any "মন্তব্য করুন" or comment count trigger buttons
    document.querySelectorAll(".open-comments-modal-btn").forEach(btn => {
        if (btn.dataset.modalBound) return;
        btn.dataset.modalBound = "true";

        btn.addEventListener("click", async function (e) {
            e.preventDefault();
            const postId = this.getAttribute("data-post-id");
            if (!postId) return;

            // Show loading skeleton in the center of the screen
            modalContainer.innerHTML = `
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content p-5 text-center border-0 shadow-lg" style="border-radius: 20px;">
                        <div class="spinner-border text-success mx-auto mb-3" style="width: 3rem; height: 3rem;" role="status">
                            <span class="visually-hidden">লোড হচ্ছে...</span>
                        </div>
                        <h5 class="fw-bold text-dark mb-1">মন্তব্য ও সমাধানসমূহ লোড হচ্ছে...</h5>
                        <p class="text-muted small mb-0">অনুগ্রহ করে কিছুক্ষণ অপেক্ষা করুন</p>
                    </div>
                </div>
            `;

            if (!bsModalInstance) {
                bsModalInstance = new bootstrap.Modal(modalContainer, { backdrop: true, keyboard: true });
            }
            bsModalInstance.show();

            try {
                const res = await fetch(`/Community/GetCommentsModal/${postId}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (res.ok) {
                    const html = await res.text();
                    modalContainer.innerHTML = html;

                    // Bind form, inputs and interactions inside the newly loaded modal
                    bindModalForm(postId);
                    initReactionButtons();
                    initAcceptSolutionButtons();
                    initAudioPlayers();
                } else {
                    modalContainer.innerHTML = `
                        <div class="modal-dialog modal-dialog-centered">
                            <div class="modal-content p-4 text-center border-0 shadow-lg" style="border-radius: 20px;">
                                <i class="bi bi-exclamation-triangle-fill text-danger fs-1 mb-2"></i>
                                <h5 class="fw-bold text-dark">মন্তব্য লোড করা সম্ভব হয়নি</h5>
                                <p class="text-muted small">সার্ভারের সাথে সংযোগে সমস্যা হয়েছে। আবার চেষ্টা করুন।</p>
                                <button type="button" class="btn btn-secondary rounded-pill px-4 mx-auto" data-bs-dismiss="modal">বন্ধ করুন</button>
                            </div>
                        </div>
                    `;
                }
            } catch (err) {
                console.error("Error loading comments modal:", err);
            }
        });
    });

    function bindModalForm(postId) {
        const form = modalContainer.querySelector(".modal-comment-form");
        const fileInput = modalContainer.querySelector(".modal-comment-image-input");
        const fileLabel = modalContainer.querySelector(".modal-selected-file-name");
        const textarea = modalContainer.querySelector(".modal-comment-textarea");
        const commentsList = document.getElementById(`modal-comments-list-${postId}`);
        const countBadge = document.getElementById("modal-comment-badge-count");

        if (fileInput && fileLabel) {
            fileInput.addEventListener("change", function () {
                if (this.files.length > 0) {
                    fileLabel.textContent = this.files[0].name;
                } else {
                    fileLabel.textContent = "";
                }
            });
        }

        if (form) {
            form.addEventListener("submit", async function (e) {
                e.preventDefault();

                if (!textarea || !textarea.value.trim()) return;

                const submitBtn = form.querySelector("button[type='submit']");
                if (submitBtn) submitBtn.disabled = true;

                const formData = new FormData(form);
                const tokenInput = form.querySelector("input[name='__RequestVerificationToken']") || document.querySelector("input[name='__RequestVerificationToken']");
                const token = tokenInput ? tokenInput.value : "";

                try {
                    const res = await fetch("/Community/AddComment", {
                        method: "POST",
                        headers: {
                            "RequestVerificationToken": token,
                            "X-Requested-With": "XMLHttpRequest"
                        },
                        body: formData
                    });

                    if (res.ok) {
                        const commentHtml = await res.text();

                        if (commentsList) {
                            const emptyMsg = commentsList.querySelector(".modal-no-comments-msg");
                            if (emptyMsg) emptyMsg.remove();

                            const tempDiv = document.createElement("div");
                            tempDiv.innerHTML = commentHtml.trim();
                            const newCommentEl = tempDiv.firstChild;
                            commentsList.appendChild(newCommentEl);

                            // Reset form
                            textarea.value = "";
                            if (fileInput) fileInput.value = "";
                            if (fileLabel) fileLabel.textContent = "";

                            // Re-init actions for newly added comment
                            initReactionButtons();
                            initAcceptSolutionButtons();
                            initAudioPlayers();

                            // Update Modal comment badge count
                            if (countBadge) {
                                const currentCount = parseInt(countBadge.textContent) || 0;
                                countBadge.textContent = `${currentCount + 1} টি`;
                            }

                            // Update Feed Card comment count in real-time
                            const feedCard = document.getElementById(`post-card-${postId}`);
                            if (feedCard) {
                                const feedCounter = feedCard.querySelector(".comment-count-val");
                                if (feedCounter) {
                                    const val = parseInt(feedCounter.textContent) || 0;
                                    feedCounter.textContent = (val + 1).toString();
                                }
                            }

                            // Scroll smoothly to newly posted comment
                            const modalBody = modalContainer.querySelector(".modal-body");
                            if (modalBody) {
                                modalBody.scrollTo({ top: modalBody.scrollHeight, behavior: "smooth" });
                            }
                        }
                    } else if (res.status === 401) {
                        window.location.href = "/Account/Login";
                    }
                } catch (err) {
                    console.error("Modal comment submission failed:", err);
                } finally {
                    if (submitBtn) submitBtn.disabled = false;
                }
            });
        }
    }
}

/**
 * 7. In-page Quick Comment Forms (if present)
 */
function initCommentForms() {
    document.querySelectorAll(".community-comment-form").forEach(form => {
        form.addEventListener("submit", async function (e) {
            e.preventDefault();

            const postId = this.getAttribute("data-post-id");
            const textarea = this.querySelector("textarea[name='Content']");
            if (!textarea || !textarea.value.trim()) return;

            const submitBtn = this.querySelector("button[type='submit']");
            if (submitBtn) submitBtn.disabled = true;

            const formData = new FormData(this);
            const tokenInput = this.querySelector("input[name='__RequestVerificationToken']");
            const token = tokenInput ? tokenInput.value : "";

            try {
                const res = await fetch("/Community/AddComment", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": token,
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData
                });

                if (res.ok) {
                    const html = await res.text();
                    const commentsList = document.getElementById(`comments-list-${postId}`);
                    if (commentsList) {
                        const emptyMsg = commentsList.querySelector(".no-comments-msg");
                        if (emptyMsg) emptyMsg.remove();

                        const tempDiv = document.createElement("div");
                        tempDiv.innerHTML = html.trim();
                        const newCommentEl = tempDiv.firstChild;
                        commentsList.appendChild(newCommentEl);

                        // Reset input
                        textarea.value = "";
                        const fileInput = this.querySelector(".comment-image-input");
                        if (fileInput) fileInput.value = "";
                        const fileLabel = this.querySelector(".selected-file-name");
                        if (fileLabel) fileLabel.textContent = "";

                        // Re-initialize dynamic buttons in new comment
                        initReactionButtons();
                        initAcceptSolutionButtons();
                        initAudioPlayers();

                        // Increment comment counter on card
                        const card = document.getElementById(`post-card-${postId}`);
                        const counter = card ? card.querySelector(".comment-count-val") : null;
                        if (counter) {
                            const val = parseInt(counter.textContent) || 0;
                            counter.textContent = (val + 1).toString();
                        }
                    }
                } else if (res.status === 401) {
                    window.location.href = "/Account/Login";
                }
            } catch (err) {
                console.error("Comment submit error:", err);
            } finally {
                if (submitBtn) submitBtn.disabled = false;
            }
        });
    });

    // File name preview inside comment form
    document.querySelectorAll(".comment-image-input").forEach(input => {
        input.addEventListener("change", function () {
            const form = this.closest(".community-comment-form");
            const label = form ? form.querySelector(".selected-file-name") : null;
            if (label && this.files.length > 0) {
                label.textContent = this.files[0].name;
            }
        });
    });
}

/**
 * 8. Accept Solution Workflow
 */
function initAcceptSolutionButtons() {
    document.querySelectorAll(".accept-solution-btn").forEach(btn => {
        if (btn.dataset.solutionBound) return;
        btn.dataset.solutionBound = "true";

        btn.addEventListener("click", async function () {
            const postId = this.getAttribute("data-post-id");
            const commentId = this.getAttribute("data-comment-id");

            if (!confirm("আপনি কি নিশ্চিত যে এই মন্তব্যটি আপনার ফসলের সমস্যার সঠিক সমাধান করেছে?")) {
                return;
            }

            const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");
            const token = tokenInput ? tokenInput.value : "";

            const formData = new FormData();
            formData.append("postId", postId);
            formData.append("commentId", commentId);

            try {
                const res = await fetch("/Community/AcceptSolution", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": token,
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData
                });

                if (res.ok) {
                    const data = await res.json();
                    if (data.success) {
                        alert("সঠিক সমাধান সফলভাবে গৃহীত হয়েছে! সমাধানকারী ৫০ কৃষি পয়েন্ট অর্জন করেছেন।");
                        window.location.reload();
                    } else {
                        alert(data.message || "সমাধান গ্রহণ করা সম্ভব হয়নি।");
                    }
                }
            } catch (err) {
                console.error("Accept solution error:", err);
            }
        });
    });
}

/**
 * 9. Bookmark Post Toggle
 */
function initBookmarkButtons() {
    document.querySelectorAll(".bookmark-toggle-btn").forEach(btn => {
        if (btn.dataset.bookmarkBound) return;
        btn.dataset.bookmarkBound = "true";

        btn.addEventListener("click", async function () {
            const postId = this.getAttribute("data-post-id");
            if (!postId) return;

            const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");
            const token = tokenInput ? tokenInput.value : "";

            const formData = new FormData();
            formData.append("postId", postId);

            try {
                const res = await fetch("/Community/ToggleBookmark", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": token,
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData
                });

                if (res.ok) {
                    const data = await res.json();
                    if (data.success) {
                        const icon = this.querySelector("i");
                        const span = this.querySelector("span");
                        if (data.isBookmarked) {
                            this.classList.remove("text-muted");
                            this.classList.add("text-success", "fw-bold");
                            if (icon) { icon.classList.remove("bi-bookmark"); icon.classList.add("bi-bookmark-fill"); }
                            if (span) span.textContent = "সংরক্ষিত থেকে সরান";
                        } else {
                            this.classList.remove("text-success", "fw-bold");
                            this.classList.add("text-muted");
                            if (icon) { icon.classList.remove("bi-bookmark-fill"); icon.classList.add("bi-bookmark"); }
                            if (span) span.textContent = "পোস্ট সংরক্ষণ করুন";
                        }
                    }
                } else if (res.status === 401) {
                    window.location.href = "/Account/Login";
                }
            } catch (err) {
                console.error("Bookmark toggle failed:", err);
            }
        });
    });
}

/**
 * 10. Copy Post Permalink
 */
function initCopyLinkButtons() {
    document.querySelectorAll(".copy-link-btn").forEach(btn => {
        if (btn.dataset.copyBound) return;
        btn.dataset.copyBound = "true";

        btn.addEventListener("click", function () {
            const relativeLink = this.getAttribute("data-link");
            const fullUrl = window.location.origin + relativeLink;
            navigator.clipboard.writeText(fullUrl).then(() => {
                alert("পোস্টের লিংক ক্লিপবোর্ডে কপি করা হয়েছে!");
            });
        });
    });
}

/**
 * 11. Report Content / Post Violation
 */
function initReportPostButtons() {
    document.querySelectorAll(".report-post-btn").forEach(btn => {
        if (btn.dataset.reportBound) return;
        btn.dataset.reportBound = "true";

        btn.addEventListener("click", async function () {
            const postId = this.getAttribute("data-post-id");
            if (!postId) return;

            const reason = prompt("এই পোস্টটির বিরুদ্ধে রিপোর্ট করার কারণ লিখুন (যেমন: ভুল বা বিভ্রান্তিকর তথ্য, অনুপযুক্ত বিষয়বস্তু):");
            if (!reason || !reason.trim()) return;

            const tokenInput = document.querySelector("input[name='__RequestVerificationToken']");
            const token = tokenInput ? tokenInput.value : "";

            const formData = new FormData();
            formData.append("postId", postId);
            formData.append("reason", reason.trim());

            try {
                const res = await fetch("/Community/ReportPost", {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken": token,
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: formData
                });

                if (res.ok) {
                    const data = await res.json();
                    alert(data.message || "রিপোর্টটি সফলভাবে জমা নেওয়া হয়েছে।");
                } else if (res.status === 401) {
                    window.location.href = "/Account/Login";
                } else {
                    alert("রিপোর্ট জমা নেওয়া যায়নি। অনুগ্রহ করে পরে আবার চেষ্টা করুন।");
                }
            } catch (err) {
                console.error("Report post error:", err);
            }
        });
    });
}
