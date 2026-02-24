/**
 * Main Application Entry Point
 * Orchestrates all polling and UI updates
 */

class TournamentDashboard {
    constructor() {
        this.tournamentStatus = {
            currentEvent: null,
            totalEvents: 0,
            isRunning: false,
            startTime: null
        };

        this.selectedEvent = null;
        this.selectedGroup = null;

        this.data = {
            events: [],
            leaders: [],
            groups: {},
            groupDetails: {}
        };

        this.init();
    }

    isInProgressStatus(statusValue) {
        return String(statusValue || '').toLowerCase() === 'inprogress';
    }

    isExpectedPollingError(error) {
        return error?.name === 'AbortError' || error?.name === 'TimeoutError' || error?.isTimeout === true;
    }

    /**
     * Initialize the dashboard
     */
    init() {
        console.log('Initializing Tournament Dashboard...');
        
        // Setup event listeners
        this.setupEventListeners();

        // Start clock immediately
        this.startClock();

        // Start all polling intervals
        this.startPolling();

        console.log('Dashboard initialized');
    }

    /**
     * Setup event listeners for user interactions
     */
    setupEventListeners() {
        // Event tab click handler
        document.addEventListener('click', (e) => {
            // Event tabs
            if (e.target.classList.contains('event-tab') || e.target.closest('.event-tab')) {
                const tab = e.target.classList.contains('event-tab') ? e.target : e.target.closest('.event-tab');
                
                // Prevent clicking disabled tabs (not-started events)
                if (tab && (tab.disabled || tab.classList.contains('disabled'))) {
                    e.preventDefault();
                    e.stopPropagation();
                    uiRenderer.showError("This event hasn't started yet");
                    return;
                }
                
                if (tab) {
                    const eventName = tab.dataset.event;
                    this.selectEvent(eventName);
                }
            }

            // Group items
            if (e.target.classList.contains('group-item') || e.target.closest('.group-item')) {
                const groupEl = e.target.classList.contains('group-item') ? e.target : e.target.closest('.group-item');
                if (groupEl) {
                    const groupLabel = groupEl.dataset.group;
                    this.selectGroup(groupLabel);
                }
            }
        });

        // Cleanup on page unload
        window.addEventListener('beforeunload', () => {
            this.cleanup();
        });
    }

    /**
     * Start real-time clock updates
     */
    startClock() {
        // Update immediately
        uiRenderer.updateIsraelTime();

        // Update every second
        pollingManager.start(
            'clock',
            () => {
                uiRenderer.updateIsraelTime();
                return Promise.resolve();
            },
            POLLING_INTERVALS.CLOCK
        );
    }

    /**
     * Start all polling intervals
     */
    startPolling() {
        // Connection status (5 sec)
        pollingManager.start(
            'connection',
            () => this.pollConnection(),
            POLLING_INTERVALS.CONNECTION,
            true
        );

        // Tournament status (5 sec)
        pollingManager.start(
            'tournamentStatus',
            () => this.pollTournamentStatus(),
            POLLING_INTERVALS.TOURNAMENT_STATUS,
            true
        );

        // Events (10 sec)
        pollingManager.start(
            'events',
            () => this.pollEvents(),
            POLLING_INTERVALS.EVENTS,
            true
        );

        // Leaders (5 sec) - start immediately; will show data when available
        this.startLeadersPolling();

        // Groups (only after event selected, 10 sec)
        // Will be started when event is selected

        // Leaders (5 sec, only when running)
        // Will be started when tournament starts

        // Group details (only after group selected, 5 sec)
        // Will be started when group is selected
    }

    /* ========================================================================
       POLLING FUNCTIONS
       ======================================================================== */

    /**
     * Poll connection status
     */
    async pollConnection() {
        try {
            const data = await apiClient.getConnection();
            const isConnected = data?.connected !== false;
            if (!isConnected) {
                uiRenderer.showError('Connection lost!');
            }
        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error('Connection poll error:', error);
        }
    }

    /**
     * Poll tournament status
     */
    async pollTournamentStatus() {
        try {
            const data = await apiClient.getStatus();
            
            if (!data) return;

            const oldRunningState = this.tournamentStatus.isRunning;
            
            this.tournamentStatus = {
                currentEvent: {
                    name: data.currentEventName,
                    index: data.currentEventIndex,
                    status: data.status
                },
                totalEvents: data.totalEvents,
                isRunning: this.isInProgressStatus(data.status),
                startTime: data.scheduledStartTime,
                status: data.status
            };

            // Update UI
            uiRenderer.updateStatusBadge(this.tournamentStatus);

            console.log(`[Status] status=${data.status}, isRunning=${this.tournamentStatus.isRunning}, totalEvents=${data.totalEvents}, currentEvent=${data.currentEventName}`);

            // When tournament finishes, do a final leaders poll and auto-select event
            if (oldRunningState && !this.tournamentStatus.isRunning) {
                // Final poll to get completed standings
                this.pollLeaders();
                
                // Auto-select first event when tournament ends
                this.onTournamentCompleted();
            }

        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error('Tournament status poll error:', error);
        }
    }

    /**
     * Poll events
     */
    async pollEvents() {
        try {
            const data = await apiClient.getEvents();
            
            if (!data || !Array.isArray(data)) {
                console.warn('[Events] No data or not array:', data);
                return;
            }

            console.log(`[Events] Received ${data.length} events:`, data.length > 0 ? data.map(e => `${e.eventName}(${e.status})`).join(', ') : 'none');

            // Always preserve previous events when API returns empty - avoids flashing
            // empty UI during brief gaps (e.g., between tournament init and series start)
            if (data.length === 0) {
                console.log('[Events] Empty events received - preserving previous events');
            } else {
                this.data.events = data;
            }

            // Always render from cached events data (which may be preserved from prior poll)
            const eventsToRender = this.data.events;
            if (eventsToRender && eventsToRender.length > 0) {
                // Update event progress
                uiRenderer.updateEventProgress(eventsToRender);

                // Update event champions
                uiRenderer.updateEventChampions(eventsToRender);

                // Update event tabs (select first if none selected)
                if (!this.selectedEvent) {
                    const firstEventName = eventsToRender[0].eventName || eventsToRender[0].name;
                    if (firstEventName) {
                        this.selectEvent(firstEventName);
                    }
                }

                uiRenderer.updateEventTabs(eventsToRender,
                    this.selectedEvent ? { eventName: this.selectedEvent } : null
                );
            }

        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error('Events poll error:', error);
        }
    }

    /**
     * Start polling leaders (called when tournament starts)
     */
    startLeadersPolling() {
        pollingManager.start(
            'leaders',
            () => this.pollLeaders(),
            POLLING_INTERVALS.LEADERS,
            true
        );
    }

    /**
     * Poll leaders
     */
    async pollLeaders() {
        try {
            const data = await apiClient.getLeaders();
            
            if (!data || !Array.isArray(data)) {
                console.warn('[Leaders] No data or not array:', data);
                return;
            }

            console.log(`[Leaders] Received ${data.length} leaders`, data.length > 0 ? `top: ${data[0]?.teamName} (${data[0]?.totalPoints} pts)` : '');

            this.data.leaders = data;

            // Always show leaders when we have data
            uiRenderer.updateOverallLeaders(data, true);

        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error('Leaders poll error:', error);
        }
    }

    /**
     * Start polling groups (called when event is selected)
     */
    startGroupsPolling(eventName) {
        pollingManager.start(
            `groups-${eventName}`,
            () => this.pollGroups(eventName),
            POLLING_INTERVALS.GROUPS,
            true
        );
    }

    /**
     * Stop polling groups for a specific event
     */
    stopGroupsPolling(eventName) {
        pollingManager.stop(`groups-${eventName}`);
    }

    /**
     * Poll groups for selected event
     */
    async pollGroups(eventName) {
        try {
            const data = await apiClient.getGroups(eventName);
            
            if (!data || !Array.isArray(data)) {
                console.warn(`[Groups] No data for event ${eventName}:`, data);
                return;
            }

            console.log(`[Groups] Received ${data.length} groups for ${eventName}:`, data.map(g => g.groupLabel || g.groupName || g.name || '?').join(', '));

            // Always preserve when no groups - avoids clearing UI during brief gaps
            if (data.length === 0) {
                console.log(`[Groups] Empty groups for ${eventName} - preserving previous groups`);
            } else {
                this.data.groups[eventName] = data;

                // Update group list
                uiRenderer.updateGroupList(data, this.selectedGroup);

                const groupLabels = data
                    .map(group => typeof group === 'string'
                        ? group
                        : (group.groupLabel || group.groupName || group.name || ''))
                    .filter(label => !!label);

                // Auto-select first group if none is selected
                if (!this.selectedGroup && groupLabels.length > 0) {
                    console.log(`[Groups] Auto-selecting first group: ${groupLabels[0]}`);
                    this.selectGroup(groupLabels[0]);
                }

                // Clear selection only if current group truly no longer exists
                if (this.selectedGroup && !groupLabels.some(label =>
                    String(label).toLowerCase() === String(this.selectedGroup).toLowerCase())) {
                    this.selectGroup(null);
                }
            }

        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error(`Groups poll error for ${eventName}:`, error);
        }
    }

    /**
     * Start polling group details (called when group is selected)
     */
    startGroupDetailsPolling(eventName, groupLabel) {
        const pollName = `groupDetails-${eventName}-${groupLabel}`;
        pollingManager.start(
            pollName,
            () => this.pollGroupDetails(eventName, groupLabel),
            POLLING_INTERVALS.STANDINGS,
            true
        );
    }

    /**
     * Stop polling group details
     */
    stopGroupDetailsPolling(eventName, groupLabel) {
        const pollName = `groupDetails-${eventName}-${groupLabel}`;
        pollingManager.stop(pollName);
    }

    /**
     * Poll group details (standings and matches)
     */
    async pollGroupDetails(eventName, groupLabel) {
        try {
            const data = await apiClient.getGroupDetails(eventName, groupLabel);
            
            if (!data) return;

            const key = `${eventName}::${groupLabel}`;
            this.data.groupDetails[key] = data;

            // Update standings
            uiRenderer.updateGroupStandings(data.groupStanding || data.standings || [], groupLabel);

            // Update matches
            uiRenderer.updateMatches(data.recentMatches || data.matches || []);

        } catch (error) {
            if (this.isExpectedPollingError(error)) return;
            console.error(`Group details poll error for ${eventName}/${groupLabel}:`, error);
        }
    }

    /* ========================================================================
       USER INTERACTION HANDLERS
       ======================================================================== */

    /**
     * Handle event selection
     */
    selectEvent(eventName) {
        // Stop previous groups polling
        if (this.selectedEvent) {
            this.stopGroupsPolling(this.selectedEvent);
        }

        // Clear previously selected group
        if (this.selectedGroup) {
            this.stopGroupDetailsPolling(this.selectedEvent, this.selectedGroup);
            this.selectedGroup = null;
        }

        // Set new selected event
        this.selectedEvent = eventName;

        // Update active tab styling
        document.querySelectorAll('.event-tab').forEach(tab => {
            tab.classList.toggle('active', tab.dataset.event === eventName);
        });

        // Start polling groups for this event
        this.startGroupsPolling(eventName);

        // Clear standings/matches
        uiRenderer.updateGroupStandings([], null);
        uiRenderer.updateMatches([]);

        console.log(`Event selected: ${eventName}`);
    }

    /**
     * Handle group selection
     */
    selectGroup(groupLabel) {
        // Stop previous group details polling
        if (this.selectedGroup && this.selectedEvent) {
            this.stopGroupDetailsPolling(this.selectedEvent, this.selectedGroup);
        }

        // Set new selected group
        this.selectedGroup = groupLabel;

        // Update active group styling
        document.querySelectorAll('.group-item').forEach(item => {
            item.classList.toggle('active', item.dataset.group === groupLabel);
        });

        if (groupLabel && this.selectedEvent) {
            // Start polling group details
            this.startGroupDetailsPolling(this.selectedEvent, groupLabel);
        } else {
            // Clear standings/matches if group deselected
            uiRenderer.updateGroupStandings([], null);
            uiRenderer.updateMatches([]);
        }

        console.log(`Group selected: ${groupLabel}`);
    }

    /**
     * Handle tournament completion - auto-select first event and group
     */
    onTournamentCompleted() {
        console.log('Tournament completed! Auto-selecting first event and group...');
        
        // Wait a moment for data to be available
        setTimeout(() => {
            // Auto-select first event if any exist
            if (this.data.events && this.data.events.length > 0) {
                const firstEvent = this.data.events[0];
                const eventName = typeof firstEvent === 'string' 
                    ? firstEvent 
                    : (firstEvent.eventName || firstEvent.name || '');
                
                if (eventName && !this.selectedEvent) {
                    console.log(`Auto-selecting first event: ${eventName}`);
                    this.selectEvent(eventName);
                    
                    // After selecting event, wait for groups to load, then auto-select first group
                    const maxWaitTime = 3000; // 3 seconds
                    const startTime = Date.now();
                    const autoSelectGroup = () => {
                        const groups = this.data.groups[eventName];
                        if (groups && groups.length > 0) {
                            const firstGroup = groups[0];
                            const groupLabel = typeof firstGroup === 'string'
                                ? firstGroup
                                : (firstGroup.groupLabel || firstGroup.groupName || firstGroup.name || '');
                            
                            if (groupLabel && !this.selectedGroup) {
                                console.log(`Auto-selecting first group: ${groupLabel}`);
                                this.selectGroup(groupLabel);
                            }
                        } else if (Date.now() - startTime < maxWaitTime) {
                            // Keep waiting for groups to load
                            setTimeout(autoSelectGroup, 500);
                        }
                    };
                    
                    setTimeout(autoSelectGroup, 1000);
                }
            }
        }, 500);
    }

    /* ========================================================================
       LIFECYCLE
       ======================================================================== */

    /**
     * Cleanup on page unload
     */
    cleanup() {
        console.log('Cleaning up dashboard...');
        pollingManager.stopAll();
    }
}

// Initialize dashboard when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.dashboard = new TournamentDashboard();
});

// Cleanup when page is unloaded
window.addEventListener('unload', () => {
    if (window.dashboard) {
        window.dashboard.cleanup();
    }
});
