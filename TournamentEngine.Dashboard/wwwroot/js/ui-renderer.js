/**
 * UI Renderer
 * Handles all DOM updates and rendering logic
 */

class UIRenderer {
    constructor() {
        this.elements = {};
        this._countdownInterval = null;
        this._lastScheduled = null;
        this.cacheElements();
    }

    normalizeEventStatus(value) {
        return String(value || '').toLowerCase();
    }

    /**
     * Cache all important DOM elements
     */
    cacheElements() {
        Object.entries(UI_ELEMENTS).forEach(([key, selector]) => {
            const element = document.querySelector(selector);
            if (element) {
                this.elements[key] = element;
            } else {
                console.warn(`Element not found: ${selector}`);
            }
        });
    }

    /**
     * Get a cached element
     */
    getElement(name) {
        return this.elements[name];
    }

    /* ========================================================================
       HEADER & TIME UPDATES
       ======================================================================== */

    /**
     * Update Israel time display
     */
    updateIsraelTime() {
        const now = new Date();

        // Use Intl with Asia/Jerusalem timezone to display Israel time reliably
        try {
            const formatter = new Intl.DateTimeFormat('en-GB', {
                hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false, timeZone: 'Asia/Jerusalem'
            });
            const timeString = formatter.format(now);
            const element = this.getElement('currentTime');
            if (element) element.textContent = timeString;
        } catch (e) {
            // Fallback to local time if Intl/timeZone not supported
            const hours = String(now.getHours()).padStart(2, '0');
            const minutes = String(now.getMinutes()).padStart(2, '0');
            const seconds = String(now.getSeconds()).padStart(2, '0');
            const timeString = `${hours}:${minutes}:${seconds}`;
            const element = this.getElement('currentTime');
            if (element) element.textContent = timeString;
        }
    }

    /**
     * Update scheduled start display and countdown (accepts UTC ISO string)
     */
    updateCountdown(scheduledStartUtc) {
        const scheduledEl = this.getElement('scheduledStart');
        const countdownEl = this.getElement('countdownTimer');

        // Clear previous interval if scheduled changed
        if (this._lastScheduled !== scheduledStartUtc) {
            if (this._countdownInterval) {
                clearInterval(this._countdownInterval);
                this._countdownInterval = null;
            }
            this._lastScheduled = scheduledStartUtc;
        }

        if (!scheduledStartUtc) {
            if (scheduledEl) scheduledEl.textContent = '--:--:--';
            if (countdownEl) countdownEl.textContent = '--:--:--';
            return;
        }

        // Parse scheduled time (assume ISO/UTC if provided as such)
        const scheduledDate = new Date(scheduledStartUtc);
        if (isNaN(scheduledDate.getTime())) {
            if (scheduledEl) scheduledEl.textContent = 'Invalid date';
            if (countdownEl) countdownEl.textContent = '--:--:--';
            return;
        }

        // Helper to format scheduled time in Israel timezone
        const formatIsraelTime = (date) => {
            try {
                return new Intl.DateTimeFormat('en-GB', {
                    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false, timeZone: 'Asia/Jerusalem'
                }).format(date);
            } catch {
                return new Date(date).toLocaleTimeString('en-GB', { hour12: false });
            }
        };

        if (scheduledEl) scheduledEl.textContent = formatIsraelTime(scheduledDate);

        const updateCountdownDisplay = () => {
            const now = new Date();
            const diffMs = scheduledDate.getTime() - now.getTime();
            if (diffMs <= 0) {
                if (countdownEl) countdownEl.textContent = 'Started';
                if (this._countdownInterval) {
                    clearInterval(this._countdownInterval);
                    this._countdownInterval = null;
                }
                return;
            }

            const totalSeconds = Math.floor(diffMs / 1000);
            const hours = Math.floor(totalSeconds / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;

            const parts = [];
            if (hours > 0) parts.push(String(hours).padStart(2, '0'));
            parts.push(String(minutes).padStart(2, '0'));
            parts.push(String(seconds).padStart(2, '0'));

            const display = hours > 0 ? parts.join(':') : parts.slice(-2).join(':');
            if (countdownEl) countdownEl.textContent = display;
        };

        // Run immediately and then start interval if not already running
        updateCountdownDisplay();
        if (!this._countdownInterval) {
            this._countdownInterval = setInterval(updateCountdownDisplay, 1000);
        }
    }

    /**
     * Update tournament status badge
     */
    updateStatusBadge(tournamentStatus) {
        const element = this.getElement('statusBadge');
        if (!element) return;

        const state = tournamentStatus?.currentEvent?.status || TOURNAMENT_STATES.NOT_STARTED;
        const normalizedState = this.normalizeEventStatus(state);
        const statusText = normalizedState === 'notstarted' || normalizedState === 'pending'
            ? 'NOT STARTED' 
            : normalizedState === 'inprogress'
            ? 'RUNNING' 
            : 'COMPLETED';

        element.textContent = statusText;
        element.classList.toggle('running', normalizedState === 'inprogress');

        // Update final status label
        const finalStatusLabel = this.getElement('finalStatusLabel');
        if (finalStatusLabel) {
            const isFinal = normalizedState === 'completed';
            finalStatusLabel.textContent = isFinal ? '(FINAL RESULTS)' : '(NOT FINAL)';
            finalStatusLabel.classList.toggle('final', isFinal);
        }
    }

    /* ========================================================================
       EVENT PROGRESS
       ======================================================================== */

    /**
     * Update event progress bar and labels
     */
    updateEventProgress(events) {
        if (!events || events.length === 0) {
            return;
        }

        // Calculate progress percentage
        const completedCount = events.filter(e => this.normalizeEventStatus(e.status) === 'completed').length;
        const progressPercent = Math.round((completedCount / events.length) * 100);

        // Update progress bar
        const progressBar = this.getElement('progressBar');
        if (progressBar) {
            progressBar.style.width = `${progressPercent}%`;
        }

        // Update progress text
        const progressPercent2 = this.getElement('progressPercent');
        if (progressPercent2) {
            progressPercent2.textContent = `${progressPercent}% Complete`;
        }

        // Update event labels
        const labelsContainer = this.getElement('eventLabelsContainer');
        if (labelsContainer) {
            labelsContainer.innerHTML = events.map(event => {
                const status = this.normalizeEventStatus(event.status);
                const isCurrent = status === 'inprogress';
                const isCompleted = status === 'completed';
                const eventName = event.eventName || event.name || 'Unknown';
                const displayEventName = this.getShortEventName(eventName) || eventName;
                
                let statusClass = 'not-started';
                if (isCompleted) statusClass = 'completed';
                else if (isCurrent) statusClass = 'current';
                
                return `<div class="event-label ${statusClass}">${displayEventName}</div>`;
            }).join('');
        }
    }

    /* ========================================================================
       EVENT CHAMPIONS
       ======================================================================== */

    /**
     * Update event champions list
     */
    updateEventChampions(events) {
        const championsList = this.getElement('championsList');
        if (!championsList) return;

        if (!events || events.length === 0) {
            championsList.innerHTML = '<p class="empty-state">No events available</p>';
            return;
        }

        championsList.innerHTML = events.map(event => {
            const normalizedStatus = this.normalizeEventStatus(event.status);
            const eventName = event.eventName || event.name || 'Unknown';
            const displayEventName = this.getShortEventName(eventName) || eventName;
            
            let statusText = '';
            let statusClass = 'pending';
            let itemClass = 'pending';

            if (normalizedStatus === 'completed') {
                statusText = event.champion || 'Unknown';
                statusClass = 'completed';
                itemClass = 'completed-event';
            } else if (normalizedStatus === 'inprogress') {
                statusText = '\u25B6 In Progress';
                statusClass = 'in-progress';
                itemClass = 'in-progress';
            } else {
                statusText = '\u25CB Pending';
                statusClass = 'pending';
                itemClass = 'pending';
            }

            return `
                <div class="champion-item ${itemClass}">
                    <span class="champion-name">${displayEventName}</span>
                    <span class="champion-status ${statusClass}">
                        ${statusText}
                    </span>
                </div>
            `;
        }).join('');
    }

    /* ========================================================================
       OVERALL LEADERS
       ======================================================================== */

    /**
     * Update overall leaders list
     */
    updateOverallLeaders(leaders, isRunning) {
        const leadersList = this.getElement('leadersList');
        if (!leadersList) return;

        if (!leaders || leaders.length === 0) {
            leadersList.innerHTML = '<p class="empty-state">No standings available yet</p>';
            return;
        }

        // When the tournament has completed (isRunning === false), visually mark the winner
        const isFinal = isRunning === false;

        leadersList.innerHTML = leaders.slice(0, 10).map((leader, index) => {
            const points = leader.totalPoints ?? leader.points ?? 0;
            const wins = leader.totalWins ?? leader.wins ?? 0;
            const losses = leader.totalLosses ?? leader.losses ?? 0;

            // mark first place as winner only when final
            const winnerClass = (isFinal && index === 0) ? ' winner' : '';

            // Render trophy icon for final winner
            const namePrefix = (isFinal && index === 0) ? '<span class="trophy">🏆</span> ' : '';

            return `
                <div class="leader-item${winnerClass}">
                    <span class="leader-name">${index + 1}. ${namePrefix}${leader.teamName}</span>
                    <span class="leader-score">${points} pts (${wins}W/${losses}L)</span>
                </div>
            `;
        }).join('');
    }

    /* ========================================================================
       EVENT TABS
       ======================================================================== */

    /**
     * Update event tabs
     */
    updateEventTabs(events, selectedEvent) {
        const tabsContainer = this.getElement('eventTabs');
        if (!tabsContainer) return;

        if (!events || events.length === 0) {
            tabsContainer.innerHTML = '<p class="empty-state">No events</p>';
            return;
        }

        tabsContainer.innerHTML = events.map(event => {
            const eventName = event.eventName || event.name || 'Unknown';
            const displayEventName = this.getShortEventName(eventName) || eventName;
            const selectedEventName = selectedEvent?.eventName || selectedEvent?.name;
            const isActive = selectedEventName && selectedEventName === eventName;
            const status = this.normalizeEventStatus(event.status);
            const isNotStarted = status === 'notstarted' || status === 'pending';
            const isCompleted = status === 'completed';
            const isInProgress = status === 'inprogress';
            
            let statusIcon = '';
            let statusClass = '';
            if (isCompleted) {
                statusIcon = '✓';
                statusClass = 'completed';
            } else if (isInProgress) {
                statusIcon = '▶';
                statusClass = 'running';
            } else if (isNotStarted) {
                statusIcon = '○';
                statusClass = 'not-started';
            }
            
            return `
                <button class="event-tab ${isActive ? 'active' : ''} ${statusClass} ${isNotStarted ? 'disabled' : ''}" 
                        data-event="${eventName}"
                        ${isNotStarted ? 'disabled' : ''}>
                    <span class="status-icon">${statusIcon}</span>
                    ${displayEventName}
                </button>
            `;
        }).join('');

        // Set default first event if none selected
        if (!selectedEvent && events.length > 0) {
            const firstTab = tabsContainer.querySelector('.event-tab:not(.disabled)');
            if (firstTab) {
                firstTab.classList.add('active');
            }
        }
    }

    /* ========================================================================
       GROUPS
       ======================================================================== */

    /**
     * Update group list for selected event
     */
    updateGroupList(groups, selectedGroup) {
        const groupList = this.getElement('groupList');
        if (!groupList) return;

        if (!groups || groups.length === 0) {
            groupList.innerHTML = '<span class="empty-state">No groups yet</span>';
            return;
        }

        groupList.innerHTML = groups.map(group => {
            const groupLabel = typeof group === 'string'
                ? group
                : (group.groupLabel || group.groupName || group.name || 'Unknown Group');
            const isActive = selectedGroup && selectedGroup === groupLabel;
            return `
                <span class="group-item ${isActive ? 'active' : ''}" data-group="${groupLabel}">
                    ${groupLabel}
                </span>
            `;
        }).join('');
    }

    /* ========================================================================
       GROUP STANDINGS
       ======================================================================== */

    /**
     * Update group standings table
     * standings: array
     * selectedGroup: string|null
     * eventName: string|null
     */
    updateGroupStandings(standings, selectedGroup = null, eventName = null) {
        const standingsBody = this.getElement('standingsBody');
        const standingsEmpty = this.getElement('standingsEmpty');
        const table = this.getElement('standingsTable');
        const finalGroupStandingsNote = this.getElement('finalGroupStandingsNote');
        const isFinalGroupStandings = String(selectedGroup || '').toLowerCase() === 'final group-finalstandings';

        if (finalGroupStandingsNote) {
            finalGroupStandingsNote.style.display = isFinalGroupStandings ? 'block' : 'none';
        }

        if (!standingsBody || !standingsEmpty) return;

        // Update the group standings title to include event + group when available
        const gsTitleEl = this.getElement('groupStandingsTitle');
        if (gsTitleEl) {
            const shortEvent = this.getShortEventName(eventName);
            if (shortEvent && selectedGroup) {
                gsTitleEl.textContent = `GROUP STANDINGS (${shortEvent} - ${selectedGroup})`;
            } else if (shortEvent) {
                gsTitleEl.textContent = `GROUP STANDINGS (${shortEvent})`;
            } else if (selectedGroup) {
                gsTitleEl.textContent = `GROUP STANDINGS (${selectedGroup})`;
            } else {
                gsTitleEl.textContent = 'GROUP STANDINGS';
            }
        }

        if (!standings || standings.length === 0) {
            standingsEmpty.style.display = 'block';
            table.style.display = 'none';
            return;
        }

        standingsEmpty.style.display = 'none';
        table.style.display = 'table';

        // Sort by score/points descending
        const sorted = [...standings].sort((a, b) => {
            const pointsA = a.score ?? a.points ?? 0;
            const pointsB = b.score ?? b.points ?? 0;
            return pointsB - pointsA;
        });

        standingsBody.innerHTML = sorted.map((standing, index) => {
            const points = standing.score ?? standing.points ?? 0;
            return `
                <tr>
                    <td>${index + 1}</td>
                    <td>${standing.teamName || standing.name || 'Unknown'}</td>
                    <td>${standing.wins || 0}</td>
                    <td>${standing.losses || 0}</td>
                    <td>${standing.draws || 0}</td>
                    <td>${points}</td>
                </tr>
            `;
        }).join('');
    }

    /* ========================================================================
       MATCHES TABLE
       ======================================================================== */

    /**
     * Update matches table
     */
    updateMatches(matches, eventName = null, selectedGroup = null) {
        const matchesBody = this.getElement('matchesBody');
        const matchesEmpty = this.getElement('matchesEmpty');
        const table = this.getElement('matchesTable');

        // Update the matches title to include event + group when available
        const matchesTitleEl = this.getElement('matchesTitle');
        if (matchesTitleEl) {
            const shortEvent = this.getShortEventName(eventName);
            if (shortEvent && selectedGroup) {
                matchesTitleEl.textContent = `MATCHES (${shortEvent} - ${selectedGroup})`;
            } else if (shortEvent) {
                matchesTitleEl.textContent = `MATCHES (${shortEvent})`;
            } else if (selectedGroup) {
                matchesTitleEl.textContent = `MATCHES (${selectedGroup})`;
            } else {
                matchesTitleEl.textContent = 'MATCHES (CURRENT GROUP)';
            }
        }
        if (!matchesBody || !matchesEmpty) return;

        if (!matches || matches.length === 0) {
            matchesEmpty.style.display = 'block';
            table.style.display = 'none';
            return;
        }

        matchesEmpty.style.display = 'none';
        table.style.display = 'table';

        // Sort by time descending (newest first)
        const sorted = [...matches].sort((a, b) => {
            const timeA = new Date(a.completedAt || 0);
            const timeB = new Date(b.completedAt || 0);
            return timeB - timeA;
        });

        matchesBody.innerHTML = sorted.map(match => {
            const timeStr = this.formatTime(match.completedAt);
            const outcomeDisplay = this.getOutcomeDisplay(match.outcome);
            
            return `
                <tr>
                    <td>${match.bot1Name || 'Bot 1'}</td>
                    <td>${match.bot1Score || 0}</td>
                    <td>${match.bot2Name || 'Bot 2'}</td>
                    <td>${match.bot2Score || 0}</td>
                    <td>${match.winnerName || '-'}</td>
                    <td>${outcomeDisplay}</td>
                    <td>${timeStr}</td>
                </tr>
            `;
        }).join('');
    }

    /* ========================================================================
       UTILITY METHODS
       ======================================================================== */

    /**
     * Format time for display
     */
    formatTime(dateString) {
        if (!dateString) return '-';
        
        try {
            const date = new Date(dateString);
            return date.toLocaleTimeString('en-US', {
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
                hour12: false
            });
        } catch {
            return '-';
        }
    }

    /**
     * Get human-readable outcome display
     */
    getOutcomeDisplay(outcome) {
        if (!outcome) return '-';
        
        const outcomeMap = {
            'Player1Wins': 'P1 Wins',
            'Player2Wins': 'P2 Wins',
            'Draw': 'Draw'
        };

        return outcomeMap[outcome] || outcome;
    }

    /**
     * Derive a shortened event name for headings.
     * Removes trailing "Tournament #N" suffixes, e.g. "RPSLS Tournament #1" -> "RPSLS"
     */
    getShortEventName(eventName) {
        if (!eventName) return null;
        let name = String(eventName).trim();

        // Try to capture a trailing number that may follow the word 'Tournament'
        // Examples handled:
        //  - "RPSLS Tournament #1" -> base="RPSLS", num="1" => "RPSLS #1"
        //  - "ColonelBlotto Tournament 2" -> base="ColonelBlotto", num="2" => "ColonelBlotto #2"
        //  - "SecurityGame #4" -> base="SecurityGame", num="4" => "SecurityGame #4"
        const match = name.match(/^(.*?)\s*(?:[Tt]ournament)?\s*(?:[#:\-]?\s*(\d+))\s*$/);
        if (match) {
            const base = (match[1] || '').trim();
            const num = match[2];
            if (base && num) {
                return `${base} #${num}`;
            }
            if (base) {
                // If there was no number, just return the base without the word 'Tournament'
                return base;
            }
        }

        // Fallback: remove the word 'Tournament' if present and return the trimmed result
        name = name.replace(/\b[Tt]ournament\b/i, '').trim();
        return name || eventName;
    }

    /**
     * Show loading indicator
     */
    showLoading() {
        const loader = this.getElement('loadingIndicator');
        if (loader) {
            loader.classList.add('active');
        }
    }

    /**
     * Hide loading indicator
     */
    hideLoading() {
        const loader = this.getElement('loadingIndicator');
        if (loader) {
            loader.classList.remove('active');
        }
    }

    /**
     * Show error toast notification
     */
    showError(message, duration = 3000) {
        const toast = this.getElement('errorToast');
        if (!toast) return;

        toast.textContent = message;
        toast.classList.add('active');

        setTimeout(() => {
            toast.classList.remove('active');
        }, duration);
    }

    /**
     * Clear all dynamic content
     */
    clearContent() {
        const eventLabelsContainer = this.getElement('eventLabelsContainer');
        const championsList = this.getElement('championsList');
        const leadersList = this.getElement('leadersList');
        const standingsBody = this.getElement('standingsBody');
        const matchesBody = this.getElement('matchesBody');

        if (eventLabelsContainer) eventLabelsContainer.innerHTML = '';
        if (championsList) championsList.innerHTML = '<p class="empty-state">Loading...</p>';
        if (leadersList) leadersList.innerHTML = '<p class="empty-state">Loading...</p>';
        if (standingsBody) standingsBody.innerHTML = '';
        if (matchesBody) matchesBody.innerHTML = '';
    }
}

// Export as global
const uiRenderer = new UIRenderer();
