#!/usr/bin/env bash
set -euo pipefail

image_path="$1"
test_image="$(mktemp --tmpdir photobooth-partition-test.XXXXXX.img)"
trap 'rm -f "$test_image"' EXIT

cp --sparse=always "$image_path" "$test_image"
iso_bytes="$(stat -c '%s' "$image_path")"
truncate -s 5G "$test_image"

start_mib=$(( (iso_bytes + 1048575) / 1048576 + 8 ))
data_size_mib=1024
data_end_mib=$((start_mib + data_size_mib))
data_start_sector=$((start_mib * 2048))
data_size_sectors=$((data_size_mib * 2048))
persistence_start_sector=$((data_end_mib * 2048))

printf '%s : start=%s, size=%s, type=c\n%s : start=%s, type=83\n' \
  "${test_image}3" "$data_start_sector" "$data_size_sectors" \
  "${test_image}4" "$persistence_start_sector" |
  sfdisk --append --force --no-reread "$test_image"

partition_table="$(sfdisk -d "$test_image")"
grep -Fq "${test_image}3 : start=" <<<"$partition_table"
grep -Fq "${test_image}4 : start=" <<<"$partition_table"

echo "Verified PhotoBooth partition layout:"
printf '%s\n' "$partition_table"
