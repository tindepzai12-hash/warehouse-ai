from datetime import datetime
from typing import Literal
from uuid import UUID
from pydantic import BaseModel, ConfigDict, Field, field_validator

class Strict(BaseModel):
    model_config = ConfigDict(extra='forbid')

class Observation(Strict):
    reportedVisibleQuantity: int | None = Field(default=None,ge=0,le=100000)
    scanCode: str | None = Field(default=None,max_length=100)
    scanFailureCount: int | None = Field(default=None,ge=0,le=1000)

class FeedbackRequest(Strict):
    requestId: UUID
    sessionId: UUID | None = None
    taskId: str = Field(default='TEST-PICK-001',pattern=r'^TEST-[A-Z]+-\d{3}$')
    language: Literal['en','vi','zh'] = 'vi'
    locale: str | None = Field(default=None,pattern=r'^(en|vi|zh)(-[A-Za-z0-9]{2,8}){0,2}$')
    eventType: Literal['user_question','scan_success','scan_failure','shelf_mismatch','route_deviation','task_completion','summary_request'] = 'user_question'
    occurredAtUtc: datetime | None = None
    question: str = Field(min_length=1,max_length=2000)
    observation: Observation = Field(default_factory=Observation)

    @field_validator('question')
    @classmethod
    def nonempty(cls,value):
        if not value.strip():
            raise ValueError('Question cannot be blank')
        return value.strip()

    @field_validator('occurredAtUtc')
    @classmethod
    def utc(cls,value):
        if value is not None and (value.tzinfo is None or value.utcoffset().total_seconds()!=0):
            raise ValueError('Use UTC timestamp ending in Z')
        return value

class Source(Strict):
    documentId: str
    sectionId: str

class Action(Strict):
    type: Literal['none','show_station_intro','show_item_details','highlight_storage_location','show_navigation_route','request_rescan','show_picking_checklist','explain_sop_step','show_safety_warning','open_exception_report','show_task_summary']
    exceptionCode: Literal['EXC-001','EXC-002','EXC-003','EXC-004','EXC-005'] | None
    targetId: str | None

class Answer(Strict):
    message: str = Field(min_length=1,max_length=8000)
    sources: list[Source] = Field(max_length=4)
    action: Action
    needsClarification: bool
    clarifyingQuestion: str | None
    feedbackType: Literal['hint','explanation','warning','summary']
    priority: Literal['normal','high']
    cooldownSeconds: int = Field(ge=0,le=60)
