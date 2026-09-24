from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class PeriodDefinition:
    number: int
    start: str
    end: str


@dataclass(frozen=True)
class CourseBlock:
    weekday: int
    first_period: int
    last_period: int
    name: str
    code: str
    credits: float
    teacher: str
    location: str
    first_week: int = 1
    last_week: int = 16
    parity: str = "All"


@dataclass(frozen=True)
class ScheduleVariant:
    key: str
    lesson_minutes: int
    semester_start: str
    periods: tuple[PeriodDefinition, ...]
    courses: tuple[CourseBlock, ...]

    @property
    def display_name(self) -> str:
        return f"{self.key}_{self.lesson_minutes}分钟"


WEEKDAYS = ("Monday", "Tuesday", "Wednesday", "Thursday", "Friday")
WEEKDAY_LABELS = ("星期一", "星期二", "星期三", "星期四", "星期五")


VARIANTS = {
    "A": ScheduleVariant(
        key="A",
        lesson_minutes=45,
        semester_start="2026-09-07",
        periods=(
            PeriodDefinition(1, "08:05", "08:50"),
            PeriodDefinition(2, "09:00", "09:45"),
            PeriodDefinition(3, "09:55", "10:40"),
            PeriodDefinition(4, "10:50", "11:35"),
            PeriodDefinition(5, "13:35", "14:20"),
            PeriodDefinition(6, "14:30", "15:15"),
            PeriodDefinition(7, "15:25", "16:10"),
            PeriodDefinition(8, "16:20", "17:05"),
            PeriodDefinition(9, "17:15", "18:00"),
        ),
        courses=(
            CourseBlock(0, 1, 5, "数据结构与算法", "DS101", 4.0, "陈明", "A7301"),
            CourseBlock(1, 1, 2, "大学英语（3）", "EN201", 3.0, "廖勇", "C208"),
            CourseBlock(1, 4, 4, "计算机网络", "CN301", 4.0, "刘茹文", "F3216"),
            CourseBlock(2, 3, 4, "数据库原理", "DB202", 3.0, "周敏", "B210"),
            CourseBlock(3, 5, 6, "人工智能导论", "AI401", 2.0, "谢心", "A3208"),
            CourseBlock(4, 1, 1, "大学体育（羽毛球）", "PE101", 1.0, "陈燕", "羽毛球馆"),
            CourseBlock(4, 7, 9, "软件工程实践", "SE402", 3.0, "王立", "C401"),
        ),
    ),
    "B": ScheduleVariant(
        key="B",
        lesson_minutes=40,
        semester_start="2026-09-07",
        periods=(
            PeriodDefinition(1, "09:00", "09:40"),
            PeriodDefinition(2, "09:50", "10:30"),
            PeriodDefinition(3, "10:40", "11:20"),
            PeriodDefinition(4, "11:30", "12:10"),
            PeriodDefinition(5, "13:20", "14:00"),
            PeriodDefinition(6, "14:10", "14:50"),
            PeriodDefinition(7, "15:00", "15:40"),
            PeriodDefinition(8, "15:50", "16:30"),
        ),
        courses=(
            CourseBlock(0, 1, 1, "大学物理", "PH101", 3.0, "李晨", "理科楼201"),
            CourseBlock(0, 3, 3, "程序设计基础", "CS102", 3.0, "周航", "计算机楼305"),
            CourseBlock(1, 1, 4, "概率论与数理统计", "MA203", 3.0, "王宁", "B105"),
            CourseBlock(2, 2, 5, "操作系统", "CS305", 4.0, "赵敏", "计算机楼401"),
            CourseBlock(3, 6, 8, "创新创业实践", "GE208", 2.0, "孙悦", "创新中心202"),
            CourseBlock(4, 2, 2, "形势与政策", "GE101", 1.0, "吴老师", "C103"),
            CourseBlock(4, 5, 5, "计算机网络", "CN301", 4.0, "刘茹文", "F3216"),
        ),
    ),
}


def get_variant(key: str) -> ScheduleVariant:
    try:
        return VARIANTS[key.upper()]
    except KeyError as error:
        raise SystemExit(f"unknown schedule variant: {key}") from error
