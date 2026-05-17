(function () {
  function data() {
    return window.PROJECT_DOCS || {};
  }

  function el(tag, className, text) {
    const node = document.createElement(tag);

    if (className) {
      node.className = className;
    }

    if (text !== undefined && text !== null) {
      node.textContent = text;
    }

    return node;
  }

  function link(label, href, className) {
    const node = el("a", className || "link-button", label);
    node.href = href;
    return node;
  }

  function badge(label, tone) {
    return el("span", tone ? "badge " + tone : "badge", label);
  }

  function statusTone(status) {
    if (status === "resolved") {
      return "blue";
    }

    if (status === "partially-refactored" || status === "추가" || status === "watch") {
      return "amber";
    }

    if (status === "proposed" || status === "design-needed" || status === "Blocked") {
      return "rose";
    }

    return "";
  }

  function mount(mountId) {
    const node = document.getElementById(mountId);

    if (!node) {
      throw new Error("마운트 요소를 찾을 수 없습니다: " + mountId);
    }

    node.replaceChildren();
    return node;
  }

  function appendMeta(parent, label, value) {
    const row = el("div", "meta-row");
    row.append(el("span", "meta-label", label), el("span", "meta-value", value));
    parent.append(row);
  }

  function renderHero(parent, options) {
    const project = data().project || {};
    const hero = el("section", "hero");
    const main = el("div", "panel hero-main");
    const meta = el("aside", "panel hero-meta");

    main.append(el("div", "eyebrow", options.eyebrow), el("h1", "", options.title));
    main.append(el("p", "lede", options.summary));

    if (options.links && options.links.length) {
      const nav = el("div", "nav-row");
      options.links.forEach(function (item) {
        nav.append(link(item.label, item.href, item.primary ? "link-button primary" : "link-button"));
      });
      main.append(nav);
    }

    appendMeta(meta, "프로젝트", project.title || "Unity Project");
    appendMeta(meta, "생성일", data().generatedOn || "알 수 없음");
    appendMeta(meta, "원본 기준", data().sourcePolicy || "Markdown 문서가 원본입니다.");
    appendMeta(meta, "승인된 HTML 관리자", data().authorizedMaintainer || "미지정");

    hero.append(main, meta);
    parent.append(hero);
  }

  function section(parent, title, note) {
    const wrapper = el("section", "section");
    const header = el("div", "section-header");
    const titleBlock = el("div");
    titleBlock.append(el("h2", "", title));

    if (note) {
      titleBlock.append(el("p", "section-note", note));
    }

    header.append(titleBlock);
    wrapper.append(header);
    parent.append(wrapper);
    return wrapper;
  }

  function renderSimpleCards(parent, items, options) {
    const grid = el("div", options && options.twoColumn ? "grid two" : "grid");

    if (!items || items.length === 0) {
      grid.append(el("div", "empty", "설정된 항목이 없습니다."));
      parent.append(grid);
      return;
    }

    items.forEach(function (item) {
      const card = el("article", "card");
      card.append(el("h3", "", item.title));

      if (item.summary) {
        card.append(el("p", "", item.summary));
      }

      const footer = el("div", "card-footer");

      if (item.group) {
        footer.append(badge(item.group, "blue"));
      }

      if (item.status) {
        footer.append(badge(item.status, statusTone(item.status) || "amber"));
      }

      if (item.href) {
        footer.append(link("페이지 열기", item.href, "link-button primary"));
      }

      if (item.path) {
        footer.append(link("원본 MD 열기", item.path, "link-button primary"));
      }

      card.append(footer);
      grid.append(card);
    });

    parent.append(grid);
  }

  function renderList(title, items) {
    const block = el("div", "detail-block");
    block.append(el("strong", "", title));
    const list = el("ul");
    items.forEach(function (item) {
      list.append(el("li", "", item));
    });
    block.append(list);
    return block;
  }

  function renderSources(sources) {
    const block = el("div", "source-list");
    (sources || []).forEach(function (source) {
      block.append(link(source.label, source.path, "link-button primary"));
    });
    return block;
  }

  function renderDiagram(diagram) {
    const box = el("div", "diagram-box");
    box.append(el("span", "diagram-title", "Mermaid diagram"));
    const pre = el("pre", "mermaid", diagram);
    box.append(pre);
    return box;
  }

  function renderDiagramCard(parent, item) {
    const card = el("article", "system-card");
    card.append(el("h3", "", item.title));

    if (item.summary) {
      card.append(el("p", "", item.summary));
    }

    if (item.diagram) {
      card.append(renderDiagram(item.diagram));
    }

    if (item.sources && item.sources.length) {
      card.append(renderSources(item.sources));
    }

    parent.append(card);
  }

  function renderSystemCard(parent, item) {
    const card = el("article", "system-card");
    const layout = el("div", "system-layout");
    const left = el("div");
    const right = el("div");
    const detail = el("div", "detail-grid");

    left.append(el("h3", "", item.title), el("p", "", item.summary));
    detail.append(
      renderList("목적", [item.purpose]),
      renderList("핵심 책임", item.responsibilities),
      renderList("연결 시스템", item.connected),
      renderList("주의 / 경계", [item.caution])
    );
    left.append(detail, renderSources(item.sources || []));

    if (item.diagram) {
      right.append(renderDiagram(item.diagram));
    }

    layout.append(left, right);
    card.append(layout);
    parent.append(card);
  }

  function renderPipelineCard(parent, item) {
    const card = el("article", "system-card");
    const layout = el("div", "system-layout");
    const left = el("div");
    const right = el("div");
    const detail = el("div", "detail-grid");
    const lists = [
      ["사용 시점", item.when ? [item.when] : []],
      ["제작 산출물", item.outputs],
      ["작성 단계", item.steps],
      ["조정 / 밸런싱 지점", item.tuning],
      ["Unity 확인", item.unityChecks],
      ["검증 체크리스트", item.checklist],
      ["흔한 실수", item.pitfalls]
    ];

    left.append(el("h3", "", item.title), el("p", "", item.summary));

    lists.forEach(function (entry) {
      const title = entry[0];
      const values = entry[1];

      if (values && values.length) {
        detail.append(renderList(title, values));
      }
    });

    left.append(detail);

    const sources = [item.source].concat(item.related || []);
    left.append(renderSources(sources));

    if (item.diagram) {
      right.append(renderDiagram(item.diagram));
    }

    layout.append(left, right);
    card.append(layout);
    parent.append(card);
  }

  function renderBadgeRow(items, tone) {
    const row = el("div", "badge-row");
    (items || []).forEach(function (item) {
      row.append(badge(item, tone));
    });
    return row;
  }

  function renderBacklogItem(parent, item) {
    const card = el("article", "card backlog-card");
    const titleRow = el("div", "card-title-row");
    titleRow.append(el("h3", "", item.title));

    const badges = el("div", "badge-row");
    badges.append(badge(item.priority, "blue"), badge(item.status, statusTone(item.status)));
    titleRow.append(badges);

    card.append(titleRow);
    card.append(el("p", "", item.summary));
    card.append(renderList("시작 조건", [item.trigger]));
    card.append(renderSources([item.source]));
    parent.append(card);
  }

  function renderBacklogGroup(parent, group) {
    const wrapper = el("section", "section-subgroup");
    const header = el("div", "subgroup-header");
    header.append(el("h3", "", group.title));

    if (group.note) {
      header.append(el("p", "section-note", group.note));
    }

    wrapper.append(header);
    const grid = el("div", "grid two");
    (group.items || []).forEach(function (item) {
      renderBacklogItem(grid, item);
    });
    wrapper.append(grid);
    parent.append(wrapper);
  }

  function renderSessionItem(parent, item) {
    const card = el("article", "card session-card");
    const titleRow = el("div", "card-title-row");
    const titleBlock = el("div");
    titleBlock.append(el("span", "date-label", item.date), el("h3", "", item.title));
    titleRow.append(titleBlock);
    titleRow.append(renderBadgeRow(item.tags, "blue"));

    card.append(titleRow, el("p", "", item.summary), renderSources([item.source]));
    parent.append(card);
  }

  window.initializeMermaid = function initializeMermaid() {
    if (window.mermaid && typeof window.mermaid.initialize === "function") {
      try {
        window.mermaid.initialize({
          startOnLoad: false,
          theme: "dark",
          securityLevel: "strict",
          flowchart: { htmlLabels: false },
          sequence: { mirrorActors: false }
        });

        if (typeof window.mermaid.run === "function") {
          const result = window.mermaid.run({ querySelector: ".mermaid" });

          if (result && typeof result.catch === "function") {
            result.catch(function (error) {
              console.warn("Mermaid 렌더링 실패", error);
            });
          }
        } else if (typeof window.mermaid.init === "function") {
          window.mermaid.init(undefined, document.querySelectorAll(".mermaid"));
        }
      } catch (error) {
        console.warn("Mermaid 초기화 실패", error);
      }
    }
  };

  window.renderIndexPage = function renderIndexPage(mountId) {
    const root = mount(mountId);
    const project = data().project || {};

    renderHero(root, {
      eyebrow: "Presentation HTML",
      title: "프로젝트 문서 대시보드",
      summary: project.description || "사람이 빠르게 읽는 프로젝트 문서 개요판입니다.",
      links: (data().pages || []).map(function (page) {
        return { label: page.title, href: page.href, primary: page.status === "필수" };
      })
    });

    const policy = section(root, "이 폴더의 역할", "Presentation HTML은 문서 원본이 아니라 사람이 보는 해설서입니다.");
    renderSimpleCards(policy, [
      { title: "Markdown이 원본", summary: data().sourcePolicy, status: "source of truth" },
      { title: "HTML은 요약판", summary: project.caution, status: "derived" },
      { title: "관리자 승인", summary: "Presentation HTML 직접 수정은 승인된 관리자 요청이 있을 때만 수행합니다.", status: data().authorizedMaintainer }
    ]);

    const pages = section(root, "주요 페이지", "긴 설명을 복제하지 않고 핵심 구조와 작성 흐름을 연결합니다.");
    renderSimpleCards(pages, data().pages || []);

    const linksSection = section(root, "중요 원본 문서", "세부 근거와 최신 source-of-truth는 아래 Markdown을 확인합니다.");
    renderSimpleCards(linksSection, data().quickLinks || [], { twoColumn: true });
  };

  window.renderArchitectureOverview = function renderArchitectureOverview(mountId) {
    const root = mount(mountId);
    const overview = data().architectureOverview || {};

    renderHero(root, {
      eyebrow: "Architecture Overview",
      title: "아키텍처 개요",
      summary: "프로젝트의 소유권, 런타임 흐름, 자주 틀리는 경계를 UML 중심으로 훑는 사람용 구조 개요입니다. 세부 구현과 계약은 연결된 Markdown을 확인합니다.",
      links: [
        { label: "대시보드", href: "index.html" },
        { label: "작성 가이드", href: "authoring-guide.html", primary: true },
        { label: "리팩터 보드", href: "refactor-board.html" }
      ]
    });

    const map = section(root, "전체 구조 흐름", "완전한 UML이 아니라 프로젝트를 빠르게 이해하기 위한 단순화된 관계도입니다.");
    map.append(renderDiagram(overview.overviewDiagram || ""));

    if (overview.detailDiagrams && overview.detailDiagrams.length) {
      const diagrams = section(root, "핵심 흐름 UML", "컴포넌트 전체 목록이 아니라 작업자가 자주 틀리는 소유권과 런타임 흐름만 적극적으로 시각화합니다.");
      const grid = el("div", "diagram-grid");
      overview.detailDiagrams.forEach(function (item) {
        renderDiagramCard(grid, item);
      });
      diagrams.append(grid);
    }

    const systems = section(root, "시스템별 개요", "각 카드는 목적, 책임, 연결 시스템, 주의점, 원본 문서 링크만 담습니다.");
    (overview.systems || []).forEach(function (item) {
      renderSystemCard(systems, item);
    });

    initializeMermaid();
  };

  window.renderAuthoringGuide = function renderAuthoringGuide(mountId) {
    const root = mount(mountId);
    const guide = data().authoringGuide || {};

    renderHero(root, {
      eyebrow: "Authoring Guide",
      title: "콘텐츠 작성 가이드",
      summary: "새 콘텐츠를 만들 때 어떤 에셋/SO/Ink/Prefab을 만들고, 어떤 값을 조정하며, Unity에서 무엇을 확인할지 보는 제작 핸드북입니다.",
      links: [
        { label: "대시보드", href: "index.html" },
        { label: "아키텍처 개요", href: "architecture-overview.html", primary: true },
        { label: "리팩터 보드", href: "refactor-board.html" }
      ]
    });

    const common = section(root, "공통 작성 흐름", "대부분의 전투 콘텐츠는 기획 정의에서 검증까지 같은 큰 순서를 따릅니다.");
    common.append(renderDiagram(guide.overviewDiagram || ""));

    if (guide.decisionCards && guide.decisionCards.length) {
      const decisions = section(root, "작성 전 판단", "HTML은 사람용 작업 판단 도구입니다. 원문 규칙과 구현 세부사항은 Markdown을 우선합니다.");
      renderSimpleCards(decisions, guide.decisionCards);
    }

    const pipelines = section(root, "작성 파이프라인", "각 카드는 제작 산출물, 조정 지점, Unity 확인, 검증 체크리스트, 원본 Markdown 링크를 묶습니다.");
    (guide.pipelines || []).forEach(function (item) {
      renderPipelineCard(pipelines, item);
    });

    initializeMermaid();
  };

  window.renderRefactorBoard = function renderRefactorBoard(mountId) {
    const root = mount(mountId);
    const board = data().refactorBoard || {};

    renderHero(root, {
      eyebrow: "Refactor Board",
      title: "리팩터 보드",
      summary: "RefactorBacklog 우선순위와 시작 조건을 사람이 빠르게 훑어보는 파생 요약판입니다. 세부 근거와 상태 변경은 원본 Markdown을 확인합니다.",
      links: [
        { label: "대시보드", href: "index.html" },
        { label: "아키텍처 개요", href: "architecture-overview.html", primary: true },
        { label: "작성 가이드", href: "authoring-guide.html" }
      ]
    });

    const map = section(root, "우선순위 흐름", board.note);
    map.append(renderDiagram(board.overviewDiagram || ""));

    const summary = section(root, "상태 요약", "Priority는 심각도보다 실행 순서입니다. resolved 항목도 관련 작업을 시작할 때는 구조 기억으로 사용합니다.");
    renderSimpleCards(summary, board.summaryCards || []);

    const groups = section(root, "리팩터 후보", "각 항목은 현재 상태, 짧은 의미, 다시 열거나 시작할 조건, 원본 Backlog 링크만 담습니다.");
    (board.groups || []).forEach(function (group) {
      renderBacklogGroup(groups, group);
    });

    if (board.blocked && board.blocked.length) {
      const blocked = section(root, "Blocked / Not Backlog Yet", "설계 규칙이 정해지기 전에는 구현 후보로 열지 않는 항목입니다.");
      const grid = el("div", "grid two");
      board.blocked.forEach(function (item) {
        renderBacklogItem(grid, item);
      });
      blocked.append(grid);
    }

    const sources = section(root, "원본 문서", "이 페이지는 아래 Markdown을 요약합니다.");
    sources.append(renderSources(board.sources || []));

    initializeMermaid();
  };
})();
