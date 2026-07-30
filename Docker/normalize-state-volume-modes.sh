#!/bin/sh
set -eu

state_root="/app/state"

fail() {
    echo "Chummer state mode normalization refused: $1" >&2
    exit 78
}

[ "$(id -u)" = "0" ] || fail "root is required for the one-shot normalization"
[ -d "$state_root" ] || fail "the state mount is unavailable"
[ ! -L "$state_root" ] || fail "the state root cannot be a symbolic link"
[ -r /proc/self/mountinfo ] || fail "mount identity is unavailable"

awk -v root="$state_root" '
    $5 == root { found = 1 }
    END { exit found ? 0 : 1 }
' /proc/self/mountinfo || fail "the state root must be a dedicated mount"

if awk -v root="$state_root" '
    index($5, root "/") == 1 { found = 1 }
    END { exit found ? 0 : 1 }
' /proc/self/mountinfo; then
    fail "nested mounts are not accepted"
fi

test -z "$(find "$state_root" -xdev -type l -print -quit)" \
    || fail "symbolic links are not accepted"
test -z "$(find "$state_root" -xdev ! -type d ! -type f -print -quit)" \
    || fail "special files are not accepted"
test -z "$(find "$state_root" -xdev -perm /7000 -print -quit)" \
    || fail "special mode bits are not accepted"

content_digest() {
    find "$state_root" -xdev -type f -exec sha256sum -b {} + \
        | LC_ALL=C sort \
        | sha256sum \
        | cut -d ' ' -f 1
}

before_digest="$(content_digest)"
find "$state_root" -xdev -type d -exec chmod 0700 {} +
find "$state_root" -xdev -type f -exec chmod 0600 {} +

test -z "$(find "$state_root" -xdev -type d ! -perm 0700 -print -quit)" \
    || fail "directory mode verification failed"
test -z "$(find "$state_root" -xdev -type f ! -perm 0600 -print -quit)" \
    || fail "file mode verification failed"

after_digest="$(content_digest)"
[ "$before_digest" = "$after_digest" ] || fail "content changed during normalization"

printf '{"status":"passed","directoryMode":"0700","fileMode":"0600","contentSha256":"%s"}\n' \
    "$after_digest"
