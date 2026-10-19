using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.ValueObjects;

namespace Yusay.Application.Content.Commands.CreateTopic;

public sealed class CreateTopicUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ITopicRepository topicRepository,
    IAuditEventRepository auditEventRepository) : ICreateTopicUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ITopicRepository _topicRepository = topicRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<TopicDto> ExecuteAsync(CreateTopicCommand command, CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para administrar temas.");
        }

        var code = TopicCode.Create(command.Code);
        var topic = Topic.Create(code, command.Name, command.Description);

        var audit = AuditEvent.CreateTopicCreated(topic.TopicId, adminUserId.Value);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var exists = await _topicRepository.CodeExistsAsync(code.Value, cancellationToken);
            if (exists)
            {
                throw new ConflictException(ITopicRepository.DuplicateCodeConflictMessage);
            }

            await _topicRepository.AddAsync(topic, tx, cancellationToken);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        return new TopicDto(topic.TopicId, topic.Code.Value, topic.Name, topic.Description);
    }
}
