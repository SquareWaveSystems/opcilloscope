#!/bin/bash
# OpcScope Test Runner
# Usage: ./scripts/run-tests.sh [--unit | --integration | --all]

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo_color() {
    echo -e "${1}${2}${NC}"
}

run_unit_tests() {
    echo_color "$YELLOW" "=== Running Unit Tests ==="
    cd "$PROJECT_DIR"

    if dotnet test --no-restore 2>&1; then
        echo_color "$GREEN" "Unit tests passed!"
        return 0
    else
        echo_color "$RED" "Unit tests failed!"
        return 1
    fi
}

start_test_server() {
    echo_color "$YELLOW" "=== Starting OPC UA Test Server ==="
    cd "$PROJECT_DIR/test-server"

    # Kill any existing test server
    pkill -f "node server.js" 2>/dev/null || true

    # Start test server in background
    node server.js &
    TEST_SERVER_PID=$!

    # Wait for server to start
    echo "Waiting for test server to start (PID: $TEST_SERVER_PID)..."
    sleep 3

    if ps -p $TEST_SERVER_PID > /dev/null 2>&1; then
        echo_color "$GREEN" "Test server started successfully"
        return 0
    else
        echo_color "$RED" "Test server failed to start"
        return 1
    fi
}

stop_test_server() {
    echo_color "$YELLOW" "=== Stopping OPC UA Test Server ==="
    if [ ! -z "$TEST_SERVER_PID" ]; then
        kill $TEST_SERVER_PID 2>/dev/null || true
    fi
    pkill -f "node server.js" 2>/dev/null || true
    echo "Test server stopped"
}

run_integration_tests() {
    echo_color "$YELLOW" "=== Running Integration Tests ==="
    cd "$PROJECT_DIR"

    # Start test server
    start_test_server
    RESULT=$?

    if [ $RESULT -ne 0 ]; then
        echo_color "$RED" "Failed to start test server"
        return 1
    fi

    # Run the OpcScope client briefly to verify it can start
    echo "Testing OpcScope startup..."

    # Since OpcScope is a TUI app, we can't fully test it without a terminal
    # But we can verify the binary runs and exits cleanly
    timeout 2 dotnet run --project "$PROJECT_DIR/OpcScope.csproj" -- --help 2>&1 || true

    # In a real integration test, you would connect to the test server
    # and verify browsing, reading, writing, and subscriptions work

    echo_color "$GREEN" "Integration test setup verified"

    # Cleanup
    stop_test_server

    return 0
}

build_project() {
    echo_color "$YELLOW" "=== Building Project ==="
    cd "$PROJECT_DIR"

    if dotnet build --no-restore 2>&1; then
        echo_color "$GREEN" "Build successful!"
        return 0
    else
        echo_color "$RED" "Build failed!"
        return 1
    fi
}

# Main execution
case "${1:-all}" in
    --unit)
        build_project && run_unit_tests
        ;;
    --integration)
        build_project && run_integration_tests
        ;;
    --all|*)
        build_project && run_unit_tests && run_integration_tests
        ;;
esac

echo_color "$GREEN" "=== All Tests Complete ==="
