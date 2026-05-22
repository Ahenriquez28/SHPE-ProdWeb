#!/bin/bash

# ============================================================
# shpe-watch.sh — Claude Code Auto-Resume Watcher
# ============================================================
#
# WHAT THIS FILE IS:
#   A shell script that manages your Claude Code sessions.
#   It lives in your project root and you run it instead of
#   running `claude` directly.
#
# HOW IT WORKS:
#   1. Starts Claude Code in your project folder
#   2. Automatically sends "/continue" on startup so Claude Code
#      reads the state file and picks up where it left off
#   3. When Claude Code exits (token limit, crash, or you quit),
#      the script catches it and waits
#   4. You press Enter after your tokens refresh
#   5. Claude Code restarts and automatically runs /continue again
#      — no manual typing needed
#
# HOW CLAUDE CODE USES IT:
#   Claude Code doesn't "use" this script directly — this script
#   manages Claude Code from the outside. Think of it like a
#   babysitter that keeps restarting Claude Code and telling it
#   "read your notes and keep going" every time it wakes up.
#   The actual intelligence (what to build next) lives in:
#     - CLAUDE.md              ← rules and project context
#     - .claude/docs/01_PROJECT_STATE.md  ← what step we're on
#     - .claude/docs/02-08_*.md           ← detailed phase guides
#
# USAGE:
#   chmod +x shpe-watch.sh   (one time only)
#   ./shpe-watch.sh          (every time you want to work)
#
# STOP IT:
#   Ctrl+C
# ============================================================

set -e

PROJECT_DIR="$HOME/Desktop/SHPE-ProdWeb"
LOG_FILE="$PROJECT_DIR/.claude/session.log"
STATE_FILE="$PROJECT_DIR/.claude/docs/01_PROJECT_STATE.md"

# Colors for terminal output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_banner() {
    echo ""
    echo -e "${BLUE}╔══════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║     SHPE Claude Code Watcher         ║${NC}"
    echo -e "${BLUE}║  Auto-resumes after token refresh    ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════╝${NC}"
    echo ""
}

check_deps() {
    # Make sure claude is installed and findable
    if ! command -v claude &> /dev/null; then
        echo -e "${RED}Error: 'claude' not found in PATH.${NC}"
        echo "Install Claude Code: npm install -g @anthropic-ai/claude-code"
        exit 1
    fi

    # Make sure the project folder exists
    if [ ! -d "$PROJECT_DIR" ]; then
        echo -e "${RED}Error: Project not found at $PROJECT_DIR${NC}"
        echo "Update PROJECT_DIR in this script to match your folder."
        exit 1
    fi
}

log() {
    # Write timestamped log to .claude/session.log
    # Claude Code doesn't read this — it's for your debugging only
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo "$msg" >> "$LOG_FILE"
}

run_claude() {
    local session_num=$1
    echo -e "${GREEN}▶ Starting Claude Code session #${session_num}...${NC}"
    echo -e "${YELLOW}  Working directory: $PROJECT_DIR${NC}"
    echo -e "${YELLOW}  Auto-sending: /continue${NC}"
    echo ""

    log "Starting session #$session_num"

    # Every session (first and resumed) automatically sends /continue.
    # /continue tells Claude Code to:
    #   1. Read CLAUDE.md for project rules
    #   2. Read 01_PROJECT_STATE.md for current step
    #   3. Start building immediately without waiting for you to type
    #
    # The `echo "/continue" | claude` pattern pipes the command
    # directly into Claude Code's stdin on startup — no manual typing.
    #
    # --resume on sessions 2+ reuses the last conversation so Claude
    # Code has memory of what it just did before the token limit hit.
    if [ "$session_num" -eq 1 ]; then
        cd "$PROJECT_DIR" && echo "/continue" | claude
    else
        # --resume picks up the last conversation context
        cd "$PROJECT_DIR" && echo "/continue" | claude --resume
    fi

    local exit_code=$?
    log "Session #$session_num exited with code $exit_code"
    return $exit_code
}

wait_for_token_refresh() {
    local session_num=$1
    echo ""
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}  Claude Code session #${session_num} ended${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
    echo -e "  If you hit your token limit:"
    echo -e "  1. Go to ${BLUE}claude.ai${NC} and check your limit"
    echo -e "  2. Wait for the refresh (usually top of the hour)"
    echo -e "  3. Come back here and press ${GREEN}Enter${NC} to auto-resume"
    echo ""
    echo -e "  Or press ${RED}Ctrl+C${NC} to quit the watcher."
    echo ""
    echo -e "${YELLOW}  Next common refresh times:${NC}"
    show_refresh_times
    echo ""
    echo -n -e "${GREEN}  Press Enter when ready to resume → ${NC}"
    read -r
    echo ""
    echo -e "${GREEN}  Resuming... Claude Code will read its state and continue.${NC}"
    echo ""
    log "User confirmed token refresh, resuming"
}

show_refresh_times() {
    # Show the next 4 hour boundaries — Anthropic resets limits hourly
    for offset in 0 1 2 3; do
        local hour=$(date -v "+${offset}H" '+%I:00 %p' 2>/dev/null \
                  || date -d "+${offset} hour" '+%I:00 %p' 2>/dev/null)
        echo -e "    • $hour"
    done
}

auto_save_state() {
    # Append an interruption note to the state file so Claude Code
    # knows it was cut off and needs to check what it last completed
    if [ -f "$STATE_FILE" ]; then
        local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
        echo "" >> "$STATE_FILE"
        echo "## Session interrupted at $timestamp" >> "$STATE_FILE"
        echo "Token limit hit or session ended. /continue will resume." >> "$STATE_FILE"
        log "Appended interruption note to 01_PROJECT_STATE.md"
    fi
}

cleanup() {
    echo ""
    echo -e "${YELLOW}Watcher stopped. Progress saved in 01_PROJECT_STATE.md.${NC}"
    echo -e "Resume anytime: ${GREEN}./shpe-watch.sh${NC}"
    log "Watcher stopped by user"
    exit 0
}

# ─── Main loop ───────────────────────────────────────────
# This is what actually runs when you execute the script.
# It's an infinite loop: start Claude Code → wait for exit
# → wait for token refresh → restart → repeat.

trap cleanup SIGINT SIGTERM

print_banner
check_deps

mkdir -p "$(dirname "$LOG_FILE")"
log "Watcher started"

session=1

while true; do
    run_claude $session
    exit_code=$?

    # Exit code 0 on session 1 = you quit cleanly on purpose
    # Any other case = token limit or crash → restart
    if [ "$exit_code" -eq 0 ] && [ "$session" -eq 1 ]; then
        echo -e "${GREEN}Clean exit. Goodbye!${NC}"
        log "Clean exit on session 1"
        exit 0
    fi

    auto_save_state
    wait_for_token_refresh $session
    session=$((session + 1))
done