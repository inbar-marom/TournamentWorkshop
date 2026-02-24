/**
 * API Endpoints Configuration
 */
const API_BASE = '/api/tournament-engine';

const ENDPOINTS = {
    STATUS: `${API_BASE}/status`,
    EVENTS: `${API_BASE}/events`,
    LEADERS: `${API_BASE}/leaders`,
    CONNECTION: `${API_BASE}/connection`,
    GROUPS: (eventName) => `${API_BASE}/groups/${eventName}`,
    GROUP_DETAILS: (eventName, groupLabel) => `${API_BASE}/groups/${eventName}/${groupLabel}`,
    MATCHES: `${API_BASE}/matches`
};

/**
 * Polling Intervals (in milliseconds)
 */
const POLLING_INTERVALS = {
    CLOCK: 1000,              // 1 second - Israel time
    CONNECTION: 5000,         // 5 seconds - Connection status
    TOURNAMENT_STATUS: 5000,  // 5 seconds - Tournament status
    EVENTS: 10000,            // 10 seconds - Event progress and details
    GROUPS: 10000,            // 10 seconds - Groups list
    LEADERS: 5000,            // 5 seconds - Event champions and overall leaders
    STANDINGS: 5000,          // 5 seconds - Group standings and matches
};

/**
 * Tournament States
 */
const TOURNAMENT_STATES = {
    NOT_STARTED: 'NotStarted',
    IN_PROGRESS: 'InProgress',
    COMPLETED: 'Completed'
};

/**
 * Match Outcomes
 */
const MATCH_OUTCOMES = {
    PLAYER1_WINS: 'Player1Wins',
    PLAYER2_WINS: 'Player2Wins',
    DRAW: 'Draw'
};

/**
 * UI Elements Cache
 */
const UI_ELEMENTS = {
    currentTime: '#currentTime',
    statusBadge: '#statusBadge',
    finalStatusLabel: '#finalStatusLabel',
    progressBar: '#progressBar',
    progressPercent: '#progressPercent',
    eventLabelsContainer: '#eventLabelsContainer',
    championsList: '#championsList',
    leadersList: '#leadersList',
    eventTabs: '#eventTabs',
    groupList: '#groupList',
    finalGroupStandingsNote: '#finalGroupStandingsNote',
    standingsTable: '#standingsTable',
    standingsBody: '#standingsBody',
    standingsEmpty: '#standingsEmpty',
    matchesTable: '#matchesTable',
    matchesBody: '#matchesBody',
    matchesEmpty: '#matchesEmpty',
    loadingIndicator: '#loadingIndicator',
    errorToast: '#errorToast'
};

/**
 * Sort Options
 */
const SORT_OPTIONS = {
    DESC: 'desc',
    ASC: 'asc'
};
