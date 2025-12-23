#!/bin/bash

# clean.sh - Clean waf/ns-3 build artifacts

print_help() {
    echo "Usage: $0 [Mode]"
    echo
    echo "Modes:"
    echo "  (no argument)   Run ./waf clean (remove compiled binaries and object files)"
    echo "  deep            Run ./waf distclean (full cleanup: binaries + config)"
    echo "  help            Show this help message"
    echo
    echo "Examples:"
    echo "  ./clean.sh        # clean only"
    echo "  ./clean.sh deep   # full distclean"
    echo "  ./clean.sh help   # show help"
}

# Parse argument
case "$1" in
    "" )
        echo "Running: ./waf clean"
        ./waf clean
        ;;
    deep )
        echo "Running: ./waf distclean"
        ./waf distclean
        ;;
    help | -h | --help )
        print_help
        ;;
    * )
        echo "Invalid option: $1"
        print_help
        exit 1
        ;;
esac
