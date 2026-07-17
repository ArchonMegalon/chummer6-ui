#!/bin/sh
set -eu

# Data Protection writes key XML through the framework repository. A private
# process umask makes every newly-created key file owner-only (0600).
umask 077
exec dotnet Chummer.Hub.Web.dll "$@"
