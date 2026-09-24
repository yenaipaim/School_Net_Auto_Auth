from __future__ import annotations

import csv
import json
import sys
import uuid
from datetime import date, datetime, timedelta, time
from pathlib import Path

from schedule_test_data import CourseBlock, ScheduleVariant, get_variant


WEEKDAY_BY_INDEX = ("Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday")
ICS_WEEKDAY = ("MO", "TU", "WE", "TH", "FR", "SA", "SU")


def parse_time(value: str) -> time:
    return datetime.strptime(value, "%H:%M").time()


def format_time(value: str) -> str:
    return parse_time(value).strftime("%H:%M:%S")


def course_id(variant: ScheduleVariant, course: CourseBlock, period_number: int | None = None) -> str:
    suffix = variant.key
    if period_number is not None:
        suffix += f"-{period_number}"
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"{suffix}:{course.weekday}:{course.name}:{course.code}"))


def json_schedule(variant: ScheduleVariant) -> dict[str, object]:
    period_lookup = {period.number: period for period in variant.periods}
    courses = []
    for course in variant.courses:
        courses.append(
            {
                "id": course_id(variant, course),
                "name": course.name,
                "teacher": course.teacher,
                "location": course.location,
                "weekday": WEEKDAY_BY_INDEX[course.weekday],
                "startTime": format_time(period_lookup[course.first_period].start),
                "endTime": format_time(period_lookup[course.last_period].end),
                "firstWeek": course.first_week,
                "lastWeek": course.last_week,
                "parity": course.parity,
            }
        )

    return {
        "schemaVersion": 2,
        "semesterStartDate": variant.semester_start,
        "courses": courses,
        "periods": [
            {
                "number": period.number,
                "startTime": format_time(period.start),
                "endTime": format_time(period.end),
            }
            for period in variant.periods
        ],
    }


def write_json(variant: ScheduleVariant, output_path: Path) -> None:
    output_path.write_text(
        json.dumps(json_schedule(variant), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def write_csv(variant: ScheduleVariant, output_path: Path) -> None:
    period_lookup = {period.number: period for period in variant.periods}
    with output_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(
            [
                "Name",
                "Teacher",
                "Location",
                "Weekday",
                "StartTime",
                "EndTime",
                "FirstWeek",
                "LastWeek",
                "Parity",
            ]
        )
        for course in variant.courses:
            for period_number in range(course.first_period, course.last_period + 1):
                period = period_lookup[period_number]
                writer.writerow(
                    [
                        course.name,
                        course.teacher,
                        course.location,
                        WEEKDAY_BY_INDEX[course.weekday],
                        period.start,
                        period.end,
                        course.first_week,
                        course.last_week,
                        course.parity,
                    ]
                )


def escape_ics(value: str) -> str:
    return (
        value.replace("\\", "\\\\")
        .replace(";", "\\;")
        .replace(",", "\\,")
        .replace("\r\n", "\\n")
        .replace("\n", "\\n")
    )


def date_for_weekday(semester_start: date, week: int, weekday: int) -> date:
    monday = semester_start - timedelta(days=semester_start.weekday())
    return monday + timedelta(weeks=week - 1, days=weekday)


def write_ics(variant: ScheduleVariant, output_path: Path) -> None:
    semester_start = date.fromisoformat(variant.semester_start)
    period_lookup = {period.number: period for period in variant.periods}
    lines = [
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "PRODID:-//SchoolNetAutoAuth//Course Schedule//ZH-CN",
        "CALSCALE:GREGORIAN",
    ]
    for course in variant.courses:
        for period_number in range(course.first_period, course.last_period + 1):
            period = period_lookup[period_number]
            start_date = date_for_weekday(semester_start, course.first_week, course.weekday)
            end_date = date_for_weekday(semester_start, course.last_week, course.weekday)
            start_time = parse_time(period.start)
            end_time = parse_time(period.end)
            lines.extend(
                [
                    "BEGIN:VEVENT",
                    f"UID:{course_id(variant, course, period_number)}@school-net-auto-auth",
                    f"DTSTAMP:{datetime.utcnow():%Y%m%dT%H%M%SZ}",
                    f"DTSTART:{start_date:%Y%m%d}T{start_time:%H%M%S}",
                    f"DTEND:{start_date:%Y%m%d}T{end_time:%H%M%S}",
                    f"RRULE:FREQ=WEEKLY;BYDAY={ICS_WEEKDAY[course.weekday]};UNTIL={end_date:%Y%m%d}T235959",
                    f"SUMMARY:{escape_ics(course.name)}",
                    f"LOCATION:{escape_ics(course.location)}",
                    f"DESCRIPTION:教师：{escape_ics(course.teacher)}",
                    "END:VEVENT",
                ]
            )
    lines.append("END:VCALENDAR")
    output_path.write_text("\r\n".join(lines) + "\r\n", encoding="utf-8")


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: generate_schedule_test_suite.py <variant> <output_dir>")

    variant = get_variant(sys.argv[1])
    output_dir = Path(sys.argv[2]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    stem = f"课程表导入测试_{variant.key}_{variant.lesson_minutes}分钟"
    write_json(variant, output_dir / f"{stem}.json")
    write_csv(variant, output_dir / f"{stem}.csv")
    write_ics(variant, output_dir / f"{stem}.ics")
    print(output_dir)


if __name__ == "__main__":
    main()
