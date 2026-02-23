/**
 * UI Renderer
 * Handles all DOM updates and rendering logic
 */

class UIRenderer {
    constructor() {
        this.elements = {};
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
        
        // Format as HH:MM:SS (24-hour format with leading zeros)
        const hours = String(now.getHours()).padStart(2, '0');
        const minutes = String(now.getMinutes()).padStart(2, '0');
        const seconds = String(now.getSeconds()).padStart(2, '0');
        const timeString = `${hours}:${minutes}:${seconds}`;
        
        const element = this.getElement('currentTime');
        if (element) {
            element.textContent = timeString;
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
                const isCurrent = this.normalizeEventStatus(event.status) === 'inprogress';
                const eventName = event.eventName || event.name || 'Unknown';
                return `<div class="event-label ${isCurrent ? 'current' : ''}">${eventName}</div>`;
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
            let statusText = 'Pending';
            let statusClass = '';

            const normalizedStatus = this.normalizeEventStatus(event.status);
            if (normalizedStatus === 'completed') {
                statusText = event.champion || 'Pending';
                statusClass = 'completed';
            } else if (normalizedStatus === 'inprogress') {
                statusText = 'In Progress';
            }

            const eventName = event.eventName || event.name || 'Unknown';

            return `
                <div class="champion-item">
                    <span class="champion-name">${eventName}</span>
                    <span class="champion-status ${statusClass}">
                        ${normalizedStatus === 'completed' ? statusText : '◉ ' + statusText}
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

        if (!isRunning) {
            leadersList.innerHTML = '<p class="empty-state">No standings available</p>';
            return;
        }

        if (!leaders || leaders.length === 0) {
            leadersList.innerHTML = '<p class="empty-state">No teams yet</p>';
            return;
        }

        leadersList.innerHTML = leaders.slice(0, 10).map((leader, index) => {
            const points = leader.totalPoints ?? leader.points ?? 0;
            return `
                <div class="leader-item">
                    <span class="leader-name">${index + 1}. ${leader.teamName}</span>
                    <span class="leader-score">${points} pts</span>
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
                    ${eventName}
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
     */
    updateGroupStandings(standings) {
        const standingsBody = this.getElement('standingsBody');
        const standingsEmpty = this.getElement('standingsEmpty');
        const table = this.getElement('standingsTable');

        if (!standingsBody || !standingsEmpty) return;

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
    updateMatches(matches) {
        const matchesBody = this.getElement('matchesBody');
        const matchesEmpty = this.getElement('matchesEmpty');
        const table = this.getElement('matchesTable');

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
