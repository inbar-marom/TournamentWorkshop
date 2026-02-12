#!/bin/bash

###############################################################################
# Tournament Engine Console - Bash Orchestration Script
# Builds and runs the tournament engine with configuration management
#
# Usage:
#   ./run-tournament.sh [OPTIONS]
#
# Options:
#   -e, --environment ENV              Environment: Development, Staging, Production (default: Development)
#   -b, --bots-dir PATH               Path to bots directory (optional, overrides config)
#   -r, --results-dir PATH            Path to results directory (optional, overrides config)
#   -d, --dashboard BOOL              Enable dashboard service (optional, overrides config)
#   -p, --dashboard-port PORT         Dashboard port number (default: 5000)
#   -l, --log-level LEVEL             Logging level: Debug, Information, Warning, Error, Critical
#   -m, --max-parallel NUM            Max parallel matches to run
#   -c, --clean                       Force clean build (delete bin/obj)
#   -t, --no-tests                    Skip running tests
#   -v, --verbose                     Enable verbose output
#   -h, --help                        Show this help message
#
# Examples:
#   ./run-tournament.sh                                    # Run with default development settings
#   ./run-tournament.sh -e Production                      # Run in production
#   ./run-tournament.sh -b ./bots -r ./results            # Custom directories
#   ./run-tournament.sh -d true -p 8080                   # Enable dashboard
#   ./run-tournament.sh -c -l Debug                       # Clean build with debug logging
###############################################################################

set -e  # Exit on error

# Default values
ENVIRONMENT="Development"
BOTS_DIRECTORY=""
RESULTS_DIRECTORY=""
DASHBOARD_ENABLED=""
DASHBOARD_PORT=""
LOG_LEVEL="Information"
MAX_PARALLEL_MATCHES=""
CLEAN=false
NO_TESTS=false
VERBOSE=false

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'  # No Color

# Helper functions
print_info() {
    echo -e "${CYAN}[INFO]${NC}  $*"
}

print_success() {
    echo -e "${GREEN}[✓]${NC} $*"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $*"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $*"
}

show_help() {
    head -n 34 "$0" | tail -n 33
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -b|--bots-dir)
            BOTS_DIRECTORY="$2"
            shift 2
            ;;
        -r|--results-dir)
            RESULTS_DIRECTORY="$2"
            shift 2
            ;;
        -d|--dashboard)
            DASHBOARD_ENABLED="$2"
            shift 2
            ;;
        -p|--dashboard-port)
            DASHBOARD_PORT="$2"
            shift 2
            ;;
        -l|--log-level)
            LOG_LEVEL="$2"
            shift 2
            ;;
        -m|--max-parallel)
            MAX_PARALLEL_MATCHES="$2"
            shift 2
            ;;
        -c|--clean)
            CLEAN=true
            shift
            ;;
        -t|--no-tests)
            NO_TESTS=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Main script execution
main() {
    print_info "Tournament Engine Console - Bash Orchestration"
    print_info "============================================"
    print_info "Environment: $ENVIRONMENT"
    print_info "Start Time: $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""
    
    # Step 1: Validate prerequisites
    print_info "Step 1: Validating prerequisites..."
    
    if ! command -v dotnet &> /dev/null; then
        print_error "dotnet CLI not found. Please install .NET 8.0 SDK."
        return 1
    fi
    print_success ".NET SDK found: $(dotnet --version)"
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    local project_path="$script_dir/TournamentEngine.Console"
    local project_file="$project_path/TournamentEngine.Console.csproj"
    
    if [ ! -f "$project_file" ]; then
        print_error "Project file not found: $project_file"
        return 1
    fi
    print_success "Project file found"
    
    # Step 2: Clean if requested
    if [ "$CLEAN" = true ]; then
        print_info "Step 2: Cleaning build artifacts..."
        
        local bin_dir="$project_path/bin"
        local obj_dir="$project_path/obj"
        
        if [ -d "$bin_dir" ]; then
            rm -rf "$bin_dir"
            print_info "  Removed: $bin_dir"
        fi
        
        if [ -d "$obj_dir" ]; then
            rm -rf "$obj_dir"
            print_info "  Removed: $obj_dir"
        fi
        
        print_success "Build artifacts cleaned"
    else
        print_info "Step 2: Skipping clean (use --clean for full rebuild)"
    fi
    
    # Step 3: Build
    print_info "Step 3: Building TournamentEngine.Console..."
    
    if ! dotnet build "$project_file" --configuration Release; then
        print_error "Build failed"
        return 1
    fi
    print_success "Build completed successfully"
    
    # Step 4: Run tests (unless skipped)
    if [ "$NO_TESTS" = false ]; then
        print_info "Step 4: Running tests..."
        
        local test_project="$script_dir/TournamentEngine.Tests/TournamentEngine.Tests.csproj"
        
        if ! dotnet test "$test_project" --configuration Release 2>&1; then
            print_warning "Some tests failed (this may be expected for pre-existing failures)"
        else
            print_success "All tests passed"
        fi
    else
        print_info "Step 4: Skipping tests (--no-tests specified)"
    fi
    
    # Step 5: Configure environment variables
    print_info "Step 5: Configuring environment..."
    
    export ASPNETCORE_ENVIRONMENT="$ENVIRONMENT"
    export DOTNET_ENVIRONMENT="$ENVIRONMENT"
    
    [ -n "$BOTS_DIRECTORY" ] && export TOURNAMENT_BOTS_DIRECTORY="$BOTS_DIRECTORY"
    [ -n "$RESULTS_DIRECTORY" ] && export TOURNAMENT_RESULTS_DIRECTORY="$RESULTS_DIRECTORY"
    [ -n "$DASHBOARD_ENABLED" ] && export TOURNAMENT_ENABLE_DASHBOARD="$DASHBOARD_ENABLED"
    [ -n "$DASHBOARD_PORT" ] && export TOURNAMENT_DASHBOARD_PORT="$DASHBOARD_PORT"
    [ -n "$LOG_LEVEL" ] && export TOURNAMENT_LOG_LEVEL="$LOG_LEVEL"
    [ -n "$MAX_PARALLEL_MATCHES" ] && export TOURNAMENT_MAX_PARALLEL_MATCHES="$MAX_PARALLEL_MATCHES"
    
    print_success "Environment configured"
    
    if [ "$VERBOSE" = true ]; then
        echo "  ASPNETCORE_ENVIRONMENT = $ASPNETCORE_ENVIRONMENT"
    fi
    
    # Step 6: Run tournament
    print_info "Step 6: Running tournament engine..."
    print_info "============================================"
    
    local console_dll="$project_path/bin/Release/net8.0/TournamentEngine.Console.dll"
    
    if [ ! -f "$console_dll" ]; then
        print_error "Console DLL not found: $console_dll"
        return 1
    fi
    
    dotnet "$console_dll"
    local exit_code=$?
    
    print_info "============================================"
    print_info "Tournament execution completed with exit code: $exit_code"
    
    if [ $exit_code -eq 0 ]; then
        print_success "Tournament completed successfully"
    else
        print_warning "Tournament exited with non-zero code: $exit_code"
    fi
    
    print_info "End Time: $(date '+%Y-%m-%d %H:%M:%S')"
    
    return $exit_code
}

# Run main function
main
exit $?
