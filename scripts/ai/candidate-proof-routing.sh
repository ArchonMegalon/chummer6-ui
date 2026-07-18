#!/usr/bin/env bash

# Shared shell-side activation for candidate-safe proof planes. Callers keep
# their historical defaults unless at least one newly documented plane variable
# is present; once requested, every variable in that plane is mandatory.

candidate_proof_plane_requested() {
  local variable_name
  for variable_name in "$@"; do
    if [[ -v "$variable_name" ]]; then
      return 0
    fi
  done
  return 1
}

candidate_proof_require_complete_plane() {
  local plane_label="$1"
  shift
  local missing=()
  local variable_name
  for variable_name in "$@"; do
    if [[ ! -v "$variable_name" || -z "${!variable_name}" ]]; then
      missing+=("$variable_name")
    fi
  done
  if [[ ${#missing[@]} -ne 0 ]]; then
    printf '[candidate-proof-routing] FAIL: %s external plane requires non-blank %s\n' \
      "$plane_label" "$(IFS=,; printf '%s' "${missing[*]}")" >&2
    return 64
  fi
}

candidate_proof_preflight() {
  local producer="$1"
  local output_path="$2"
  local repo_root="$3"
  local release_channel_path="$4"
  local input_root="${5:-}"
  local sidecar_output="${6:-}"
  local command=(
    python3 "$repo_root/scripts/ai/candidate_proof_routing.py" preflight
    --producer "$producer"
    --output "$output_path"
    --repo-root "$repo_root"
    --release-channel "$release_channel_path"
  )
  if [[ -n "$input_root" ]]; then
    command+=(--input-root "$input_root")
  fi
  if [[ -n "$sidecar_output" ]]; then
    command+=(--sidecar-output "$sidecar_output")
  fi
  "${command[@]}"
}
