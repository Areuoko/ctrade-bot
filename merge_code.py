#!/usr/bin/env python3
"""
merge_code.py
اسکریپتی برای جمع کردن همه فایل‌های کد یک پروژه (مثلا ربات cTrader/cAlgo)
داخل یک فایل متنی واحد، تا راحت بشه همه رو یکجا فرستاد.

نحوه استفاده:
    python merge_code.py /path/to/project
    # یا اگه داخل خود پوشه پروژه هستی:
    python merge_code.py

خروجی:
    merged_code.txt  (کنار همین اسکریپت ساخته میشه)
"""

import os
import sys

# پسوندهایی که باید خونده بشن - در صورت نیاز اضافه/کم کن
EXTENSIONS = {".cs", ".py", ".json", ".xml", ".config", ".csproj", ".txt"}

# پوشه‌هایی که باید نادیده گرفته بشن
IGNORE_DIRS = {"bin", "obj", ".git", ".vs", "packages", "__pycache__", "node_modules"}

def collect_files(root_dir):
    collected = []
    for dirpath, dirnames, filenames in os.walk(root_dir):
        dirnames[:] = [d for d in dirnames if d not in IGNORE_DIRS]
        for fname in filenames:
            ext = os.path.splitext(fname)[1].lower()
            if ext in EXTENSIONS:
                collected.append(os.path.join(dirpath, fname))
    return sorted(collected)

def merge_files(file_list, root_dir, output_path):
    with open(output_path, "w", encoding="utf-8", errors="replace") as out:
        for fpath in file_list:
            rel_path = os.path.relpath(fpath, root_dir)
            out.write("\n" + "=" * 80 + "\n")
            out.write(f"FILE: {rel_path}\n")
            out.write("=" * 80 + "\n\n")
            try:
                with open(fpath, "r", encoding="utf-8", errors="replace") as f:
                    out.write(f.read())
            except Exception as e:
                out.write(f"[خطا در خواندن فایل: {e}]\n")
            out.write("\n")

def main():
    root_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    root_dir = os.path.abspath(root_dir)

    if not os.path.isdir(root_dir):
        print(f"پوشه پیدا نشد: {root_dir}")
        sys.exit(1)

    files = collect_files(root_dir)
    if not files:
        print("هیچ فایل کدی پیدا نشد. پسوندها یا مسیر رو چک کن.")
        sys.exit(1)

    output_path = os.path.join(os.getcwd(), "merged_code.txt")
    merge_files(files, root_dir, output_path)

    print(f"تعداد {len(files)} فایل با موفقیت ادغام شد.")
    print(f"خروجی ذخیره شد در: {output_path}")

if __name__ == "__main__":
    main()
