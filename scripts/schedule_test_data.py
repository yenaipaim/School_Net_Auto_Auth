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
    "C": ScheduleVariant(
        key="C",
        lesson_minutes=50,
        semester_start="2027-02-22",
        periods=(
            PeriodDefinition(1, "08:00", "08:50"),
            PeriodDefinition(2, "09:00", "09:50"),
            PeriodDefinition(3, "10:10", "11:00"),
            PeriodDefinition(4, "11:10", "12:00"),
            PeriodDefinition(5, "13:30", "14:20"),
            PeriodDefinition(6, "14:35", "15:25"),
            PeriodDefinition(7, "15:40", "16:30"),
        ),
        courses=(
            CourseBlock(0, 1, 2, "管理学原理", "BA101", 3.0, "何静", "博学楼B201"),
            CourseBlock(0, 5, 5, "商务统计分析", "BA205", 2.0, "蒋文", "实验楼E306"),
            CourseBlock(1, 3, 4, "市场营销学", "MK202", 3.0, "杜婉", "博学楼C408"),
            CourseBlock(2, 1, 1, "经济法", "LA110", 2.0, "罗强", "法学楼A102"),
            CourseBlock(3, 5, 7, "企业战略管理", "BA310", 3.0, "曹睿", "博学楼D501"),
            CourseBlock(4, 2, 2, "财务管理", "FN220", 3.0, "韩露", "财经楼A305"),
        ),
    ),
    "D": ScheduleVariant(
        key="D",
        lesson_minutes=45,
        semester_start="2027-03-01",
        periods=(
            PeriodDefinition(1, "07:50", "08:35"),
            PeriodDefinition(2, "08:45", "09:30"),
            PeriodDefinition(3, "09:50", "10:35"),
            PeriodDefinition(4, "10:45", "11:30"),
            PeriodDefinition(5, "13:10", "13:55"),
            PeriodDefinition(6, "14:05", "14:50"),
            PeriodDefinition(7, "15:10", "15:55"),
            PeriodDefinition(8, "16:05", "16:50"),
        ),
        courses=(
            CourseBlock(0, 1, 3, "工程制图", "ME102", 4.0, "彭海", "工程训练中心101", 1, 12),
            CourseBlock(0, 6, 7, "材料力学", "ME210", 3.0, "石岩", "机械楼M204", 1, 16),
            CourseBlock(1, 2, 2, "电路分析", "EE105", 4.0, "钱涛", "电气楼E302", 1, 14),
            CourseBlock(1, 5, 6, "工程材料", "ME218", 3.0, "赵峰", "机械楼M108", 3, 16, "Odd"),
            CourseBlock(2, 1, 4, "程序设计基础", "CS102", 4.0, "顾晨", "计算中心C401", 1, 16),
            CourseBlock(3, 3, 4, "概率论与数理统计", "MA203", 3.0, "马莉", "理科楼R307", 2, 16, "Even"),
            CourseBlock(4, 7, 8, "工程伦理", "GE305", 2.0, "唐洁", "人文楼H205", 1, 12),
        ),
    ),
    "E": ScheduleVariant(
        key="E",
        lesson_minutes=40,
        semester_start="2027-02-15",
        periods=(
            PeriodDefinition(1, "08:10", "08:50"),
            PeriodDefinition(2, "09:00", "09:40"),
            PeriodDefinition(3, "09:50", "10:30"),
            PeriodDefinition(4, "10:40", "11:20"),
            PeriodDefinition(5, "11:30", "12:10"),
            PeriodDefinition(6, "13:20", "14:00"),
            PeriodDefinition(7, "14:10", "14:50"),
            PeriodDefinition(8, "15:00", "15:40"),
            PeriodDefinition(9, "15:50", "16:30"),
        ),
        courses=(
            CourseBlock(0, 1, 5, "人体解剖学", "MD101", 4.0, "冯雪", "医学楼A201", 1, 16),
            CourseBlock(1, 2, 4, "生理学", "MD205", 3.0, "沈锐", "医学楼B305", 1, 15),
            CourseBlock(1, 7, 7, "医学英语", "EN310", 2.0, "许晴", "公共楼C108", 1, 12),
            CourseBlock(2, 1, 1, "组织胚胎学", "MD118", 3.0, "梁慧", "医学楼A406", 2, 16),
            CourseBlock(3, 5, 8, "病理学", "MD302", 4.0, "周宁", "医学楼D510", 1, 16),
            CourseBlock(4, 3, 3, "药理学", "MD320", 3.0, "贺雯", "药学楼P202", 1, 14),
            CourseBlock(4, 6, 6, "医学伦理学", "GE410", 2.0, "姚敏", "公共楼C305", 4, 16, "Even"),
        ),
    ),
    "F": ScheduleVariant(
        key="F",
        lesson_minutes=55,
        semester_start="2027-09-06",
        periods=(
            PeriodDefinition(1, "08:00", "08:55"),
            PeriodDefinition(2, "09:05", "10:00"),
            PeriodDefinition(3, "10:20", "11:15"),
            PeriodDefinition(4, "11:25", "12:20"),
            PeriodDefinition(5, "13:40", "14:35"),
            PeriodDefinition(6, "14:45", "15:40"),
            PeriodDefinition(7, "16:00", "16:55"),
        ),
        courses=(
            CourseBlock(0, 3, 4, "中国古代文学", "CL201", 4.0, "林月", "文学楼L301", 1, 16),
            CourseBlock(0, 7, 7, "汉语写作", "CL115", 2.0, "程然", "文学楼L102", 1, 12),
            CourseBlock(1, 1, 1, "现代汉语", "CL108", 3.0, "吴桐", "文学楼L205", 1, 16),
            CourseBlock(1, 5, 6, "外国文学史", "CL305", 3.0, "叶青", "文学楼L402", 2, 16),
            CourseBlock(2, 2, 3, "中国现当代文学", "CL220", 4.0, "郑妍", "文学楼L308", 1, 15, "Odd"),
            CourseBlock(3, 1, 2, "语言学概论", "CL210", 3.0, "范博", "文学楼L207", 1, 14),
            CourseBlock(3, 6, 7, "文学评论", "CL418", 2.0, "周岚", "文学楼L506", 5, 16),
            CourseBlock(4, 3, 5, "创意写作", "CL330", 3.0, "顾言", "创作中心W201", 1, 16),
        ),
    ),
    "G": ScheduleVariant(
        key="G",
        lesson_minutes=35,
        semester_start="2027-09-13",
        periods=(
            PeriodDefinition(1, "08:30", "09:05"),
            PeriodDefinition(2, "09:15", "09:50"),
            PeriodDefinition(3, "10:00", "10:35"),
            PeriodDefinition(4, "10:45", "11:20"),
            PeriodDefinition(5, "13:00", "13:35"),
            PeriodDefinition(6, "13:45", "14:20"),
            PeriodDefinition(7, "14:40", "15:15"),
            PeriodDefinition(8, "15:25", "16:00"),
        ),
        courses=(
            CourseBlock(0, 1, 4, "高等代数", "MA301", 4.0, "秦川", "理科楼R401", 1, 16),
            CourseBlock(1, 5, 5, "数学分析", "MA201", 4.0, "白露", "理科楼R305", 1, 16),
            CourseBlock(1, 7, 8, "Python 科学计算", "CS240", 2.0, "陆航", "计算中心C202", 1, 12),
            CourseBlock(2, 2, 3, "离散数学", "CS208", 3.0, "孙志", "理科楼R208", 1, 15),
            CourseBlock(3, 1, 1, "大学物理", "PH101", 3.0, "杨凯", "物理楼P303", 1, 16),
            CourseBlock(3, 6, 7, "数值分析", "MA408", 3.0, "罗欣", "理科楼R506", 3, 16, "Odd"),
            CourseBlock(4, 4, 5, "运筹学", "MA360", 3.0, "朱丹", "理科楼R310", 1, 14),
        ),
    ),
    "H": ScheduleVariant(
        key="H",
        lesson_minutes=45,
        semester_start="2028-02-21",
        periods=(
            PeriodDefinition(1, "08:15", "09:00"),
            PeriodDefinition(2, "09:10", "09:55"),
            PeriodDefinition(3, "10:05", "10:50"),
            PeriodDefinition(4, "11:00", "11:45"),
            PeriodDefinition(5, "13:30", "14:15"),
            PeriodDefinition(6, "14:25", "15:10"),
            PeriodDefinition(7, "15:20", "16:05"),
        ),
        courses=(
            CourseBlock(0, 1, 2, "设计素描", "AR101", 3.0, "方圆", "艺术楼A102", 1, 12),
            CourseBlock(0, 5, 7, "中外美术史", "AR210", 3.0, "段宁", "艺术楼A305", 1, 16),
            CourseBlock(1, 3, 3, "色彩构成", "AR108", 2.0, "牧云", "艺术楼B201", 1, 10),
            CourseBlock(2, 4, 5, "数字媒体设计", "DM205", 3.0, "彭悦", "设计中心D402", 2, 16),
            CourseBlock(3, 1, 3, "视觉传达基础", "VD120", 4.0, "安然", "艺术楼C301", 1, 16),
            CourseBlock(4, 6, 7, "艺术概论", "AR330", 2.0, "林晓", "艺术楼A410", 3, 15, "Even"),
        ),
    ),
}


def get_variant(key: str) -> ScheduleVariant:
    try:
        return VARIANTS[key.upper()]
    except KeyError as error:
        raise SystemExit(f"unknown schedule variant: {key}") from error
